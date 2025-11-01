using CodeStage.AntiCheat.Storage;
using SecureVariables;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using UnityEditor;
using UnityEngine;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

namespace TMG_Inventory
{
    public class SteamItemTrader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MoneyManager moneyManager;
        [SerializeField] private SteamInventoryChecker steamInventoryChecker;

        [Header("UI")]
        [SerializeField] private GameObject confirmationMenu;
        [SerializeField] private GameObject tradeDelayWindow;
        [SerializeField] private TMPro.TextMeshProUGUI tradeDelayText;
        [SerializeField] private TMPro.TextMeshProUGUI confirmationText;
        [SerializeField] private TMPro.TMP_InputField quantityInput; // Quantity input box

        private bool isTradePending = false;
        private bool success = false;
        private int pendingTradeItem = -1;
        private int pendingReward = 0;

        // Item IDs and values
        private const int copperItemId = 100;
        private const int silverItemId = 200;
        private const int goldItemId = 300;

        private const int copperValue = 50;
        private const int silverValue = 5000;
        private const int goldValue = 30000;
        private SecureInt currentItemID;
        private SecureInt currentItemQty;
        private SecureInt previewReward;
        private SecureInt previewID;

        private LevelLoader levelLoader;
        private void Start()
        {
            levelLoader = FindAnyObjectByType<LevelLoader>();

            if (moneyManager == null)
                moneyManager = FindObjectOfType<MoneyManager>();

            if (steamInventoryChecker == null)
                steamInventoryChecker = FindObjectOfType<SteamInventoryChecker>();

            if (confirmationMenu != null)
                confirmationMenu.SetActive(false);

            if (tradeDelayWindow != null)
                tradeDelayWindow.SetActive(false);
        }

        public void TradeEligibleItems(int itemToTrade)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("Steam not initialized. Cannot trade.");
                return;
            }

            if (isTradePending)
            {
                Debug.LogWarning("Trade already pending. Wait for confirmation.");
                return;
            }

            previewReward = CalculateTotalReward(itemToTrade);
            if (previewReward <= 0)
            {
                Debug.Log("No eligible Steam items found for trade.");
                return;
            }
            else
            {
                Debug.Log("preview reward " +  previewReward);  
            }

            pendingTradeItem = itemToTrade;
            if(pendingTradeItem== 0)
            {
                previewID = 100;
            }
            if (pendingTradeItem == 1)
            {
                previewID = 200;
            }
            if (pendingTradeItem == 2)
            {
                previewID = 300;
            }
            Debug.Log("preview item " + previewID);
            pendingReward = previewReward;
            isTradePending = true;

            if (confirmationMenu != null)
            {
                confirmationMenu.SetActive(true);
                if (confirmationText != null)
                {
                    string rewardText = moneyManager.ReturnMoneyAsColoredGSC_String(previewReward);
                    confirmationText.text = $"Trade these items for {rewardText}?";
                }

                if (quantityInput != null)
                {
                    quantityInput.text = ""; // clear for new trade
                    quantityInput.onValueChanged.RemoveAllListeners();
                    quantityInput.onValueChanged.AddListener(OnQuantityChanged); // update preview when changed
                }
            }
        }  // Called when user types a quantity
        private void OnQuantityChanged(string input)
        {
            if (!int.TryParse(input, out int enteredQty) || enteredQty <= 0)
            {
                confirmationText.text = "Enter a valid quantity.";
                return;
            }

            int itemId = GetItemIdFromTradeType(pendingTradeItem);
            var stacks = SteamInventoryChecker.GetSteamItemInstances(itemId);
            int totalAvailable = 0;
            foreach (var stack in stacks)
                totalAvailable += stack.quantity;

            // Clamp entered quantity
            enteredQty = Mathf.Clamp(enteredQty, 1, totalAvailable);
            currentItemQty = enteredQty;

            int valuePerUnit = pendingTradeItem switch
            {
                0 => copperValue,
                1 => silverValue,
                2 => goldValue,
                _ => 0
            };

            int newReward = enteredQty * valuePerUnit;
            string rewardText = moneyManager.ReturnMoneyAsColoredGSC_String(newReward);
            confirmationText.text = $"Trade {enteredQty} items for {rewardText}?";
            previewReward = newReward;
            currentItemQty = enteredQty;

        }

        public void ConfirmTrade()
        {
            if (!isTradePending)
                return;

            // If no input, assume all
            if (quantityInput != null && string.IsNullOrWhiteSpace(quantityInput.text))
            {
                var stacks = SteamInventoryChecker.GetSteamItemInstances(GetItemIdFromTradeType(pendingTradeItem));
                int totalQty = 0;
                foreach (var s in stacks) totalQty += s.quantity;
                currentItemQty = totalQty;
            }

            confirmationMenu.SetActive(false);
            StartCoroutine(TryTradeAction(5));
        }

        public void CancelTrade()
        {
            Debug.Log("[Steam] Trade canceled by user.");
            ResetConfirmation();
        }

        private void ResetConfirmation()
        {
            isTradePending = false;
            pendingTradeItem = -1;
            pendingReward = 0;

            if (confirmationMenu != null)
                confirmationMenu.SetActive(false);
        }

        private IEnumerator TryTradeAction(int tries)
        {
            if (tradeDelayWindow != null)
                tradeDelayWindow.SetActive(true);

            success = false;
            int targetItemId = GetItemIdFromTradeType(pendingTradeItem);
            int totalReward = 0;

            for (int i = 0; i < tries; i++)
            {
                tradeDelayText.text = $"VERIFYING TRADE: {i + 1}/{tries}";
                Debug.Log($"Trade verification attempt {i + 1} of {tries}");

                // Consume and sync Steam items
                totalReward = ExecuteTrade(pendingTradeItem);
                steamInventoryChecker.SyncSteamItems();

                yield return new WaitForSeconds(0.75f);

                // Verify deletion — if item no longer exists, mark as success
                var remainingStacks = SteamInventoryChecker.GetSteamItemInstances(targetItemId);
                if (remainingStacks == null || remainingStacks.Count == 0)
                {
                    success = true;
                    Debug.Log("Trade verified — item successfully removed.");
                    break;
                }
                Debug.Log("Item still present, retrying verification...");
                yield return new WaitForSeconds(0.75f);
            }

            // Outcome
            tradeDelayText.text = success ? "TRADE SUCCESSFUL" : "TRADE FAILED";
            if (success)
            {

                RewardPlayer();
                Debug.Log("[Steam] Trade completed and verified.");
                //inventoryDebugger.RefreshStacks();
                //steamInventoryChecker.SyncSteamItems();
            }
            else
            {
                Debug.LogWarning("[Steam] Trade failed to verify item removal. Aborting reward.");
            }

            yield return new WaitForSeconds(1f);
            if (tradeDelayWindow != null)
                tradeDelayWindow.SetActive(false);

            ResetConfirmation();
        }
        private int GetItemIdFromTradeType(int tradeType)
        {
            return tradeType switch
            {
                0 => copperItemId,
                1 => silverItemId,
                2 => goldItemId,
                _ => -1,
            };
        }

        private int CalculateTotalReward(int itemToTrade)
        {
            int totalReward = 0;
            if (itemToTrade == 0)
                totalReward += CalculateItemValue(copperItemId, copperValue);
            else if (itemToTrade == 1)
                totalReward += CalculateItemValue(silverItemId, silverValue);
            else if (itemToTrade == 2)
                totalReward += CalculateItemValue(goldItemId, goldValue);
            return totalReward;
        }

        private int CalculateItemValue(int itemId, int valuePerUnit)
        {
            var stacks = SteamInventoryChecker.GetSteamItemInstances(itemId);
            int total = 0;
            foreach (var stack in stacks)
                total += stack.quantity * valuePerUnit;
            return total;
        }

        private int ExecuteTrade(int itemToTrade)
        {
            int totalReward = 0;

            if (itemToTrade == 0)
                totalReward += ConsumeAndReward(copperItemId, copperValue);
            else if (itemToTrade == 1)
                totalReward += ConsumeAndReward(silverItemId, silverValue);
            else if (itemToTrade == 2)
                totalReward += ConsumeAndReward(goldItemId, goldValue);

            return totalReward;
        }
        private int ConsumeAndReward(int itemId, int valuePerUnit)
        {
            var stacks = SteamInventoryChecker.GetSteamItemInstances(itemId);
            int totalReward = 0;
            if (success == false)
            {
                foreach (var stack in stacks)
                {
                    int qty = stack.quantity;
                    int reward = qty * valuePerUnit;
                    totalReward += reward;

                    SteamInventoryChecker.ConsumeSteamItem(stack.instanceId, currentItemQty);

                    RewardPlayer();

                    levelLoader.LoadLevel("Empty");
                    /*
#if UNITY_EDITOR
                    EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
                    */
                    //SET ITEM TOTAL
                    Debug.Log($"Trying to Consumed {currentItemQty}x item {itemId} -> +{reward} | instanceID: " + stack.instanceId);
                    //success = true;
                    currentItemID = itemId;

                }

            }
            return totalReward;
        }
        private void RewardPlayer()
        {
            moneyManager.GiveRewardMoney(previewReward);
            moneyManager.SaveMoneyData();
            //ExecuteTrade(pendingTradeItem);

            ObscuredPrefs.SetInt("Item" + currentItemID, ObscuredPrefs.GetInt("Item" + currentItemID) - currentItemQty);
            Debug.Log($"Trade finished | item ID: {currentItemID} | QTY: {currentItemQty}x | Reward: {previewReward}");
        }
    }
}
