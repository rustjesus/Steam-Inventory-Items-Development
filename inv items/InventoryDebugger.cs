using Steamworks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryDebugger : MonoBehaviour
{
    [Header("Stack Window")]
    public GameObject stackWindow;
    public GameObject makeStackWindow;
    public TMP_InputField stackSizeInput;
    [SerializeField] private Button confirmButton;

    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private TextMeshProUGUI stackCountText;
    [SerializeField] private TextMeshProUGUI stackSizesText;
    [SerializeField] private TextMeshProUGUI stackInfoText;


    [Header("stack delay menu")]
    public GameObject stackDelayWindow;
    [SerializeField] private TextMeshProUGUI stackDelayText;

    private System.Action pendingAction;

    [System.Serializable]
    public class ItemStackEntry
    {
        public int itemId;
        public int quantity;
        public int stackCount;
        public int[] stackSizes;
    }

    [SerializeField]
    private List<ItemStackEntry> debugStacks = new List<ItemStackEntry>();

    private SteamInventoryChecker steamInventoryChecker;
    private void Awake()
    {
        steamInventoryChecker = FindAnyObjectByType<SteamInventoryChecker>();
        makeStackWindow.SetActive(true);
        stackWindow.SetActive(false);
        stackDelayWindow.SetActive(false);
        stackInfoText.gameObject.SetActive(false);
        confirmButton.onClick.AddListener(OnConfirmStackAction);

    }

    private void OnValidate() => RefreshStacks();
    private void Update()
    {
        RefreshStacks();
        UpdateUIText();
    }

    private void RefreshStacks()
    {
        debugStacks.Clear();
        var stacks = InventoryManager.GetAllStacks();

        foreach (var kvp in stacks)
        {
            int id = kvp.Key;
            debugStacks.Add(new ItemStackEntry
            {
                itemId = id,
                quantity = kvp.Value,
                stackCount = InventoryManager.GetStackCount(id),
                stackSizes = InventoryManager.GetStackSizes(id)
            });
        }
    }

    private void UpdateUIText()
    {
        if (GameManager.currentItemDefToStack == 0)
        {
            quantityText.text = "Quantity: -";
            stackCountText.text = "Stack Count: -";
            stackSizesText.text = "Stacks: -";
            return;
        }

        int qty = InventoryManager.GetQuantity(GameManager.currentItemDefToStack);
        int count = InventoryManager.GetStackCount(GameManager.currentItemDefToStack);
        int[] sizes = InventoryManager.GetStackSizes(GameManager.currentItemDefToStack);

        quantityText.text = $"Quantity: {qty}";
        stackCountText.text = $"Stack Count: {count}";
        stackSizesText.text = "Stacks: " + string.Join(",", sizes);
    }

    // ----------------- BUTTON ACTIONS -----------------

    public void MakeStack()
    {
        makeStackWindow.SetActive(true);
        stackWindow.SetActive(true);
        stackSizeInput.gameObject.SetActive(true);
        pendingAction = ConfirmMakeStacksOf;
    }

    public void StackAll()
    {
        stackInfoText.gameObject.SetActive(true);
        stackInfoText.text = "STACK ALL?";
        makeStackWindow.SetActive(true);
        stackWindow.SetActive(true);
        stackSizeInput.gameObject.SetActive(false);
        pendingAction = ConfirmStackAll;
    }

    public void UnstackAll()
    {
        stackInfoText.gameObject.SetActive(true);
        stackInfoText.text = "UNSTACK ALL?";
        makeStackWindow.SetActive(true);
        stackWindow.SetActive(true);
        stackSizeInput.gameObject.SetActive(false);
        pendingAction = ConfirmUnstackAll;
    }

    private void OnConfirmStackAction()
    {
        stackWindow.SetActive(false);
        StartCoroutine(TryStackAction(5));
    }
    IEnumerator TryStackAction(int tries)
    {
        stackDelayWindow.SetActive(true);
        success = false; // reset before starting

        for (int i = 0; i < tries; i++)
        {
            int remainingTime = tries - i;
            stackDelayText.text = "STACKING ATTEMPT: " + (i + 1) + "/" + tries;
            Debug.Log("Attempt " + (i + 1) + " of " + tries);

            yield return new WaitForSeconds(0.5f);

            // Run the action
            pendingAction?.Invoke();
            steamInventoryChecker.SyncSteamItems();

            // if successful, break out early
            if (success)
            {
                Debug.Log("Stack action succeeded on attempt " + (i + 1));
                break;
            }

            yield return new WaitForSeconds(0.5f); // optional wait between tries
        }

        stackDelayText.text = success ? "STACKING COMPLETE" : "STACKING FAILED";
        Debug.Log("Stacking finished. Success: " + success);

        yield return new WaitForSeconds(1f);
        stackDelayWindow.SetActive(false);
    }

    // ----------------- ACTION IMPLEMENTATIONS -----------------

    private void ConfirmMakeStacksOf()
    {
        if (!int.TryParse(stackSizeInput.text, out int newStackSize) || newStackSize <= 0)
        {
            Debug.LogWarning("Invalid stack size.");
            return;
        }

        int itemDefId = GameManager.currentItemDefToStack;
        int totalQty = InventoryManager.GetQuantity(itemDefId);

        if (totalQty <= 0)
        {
            Debug.LogWarning("No items to stack.");
            makeStackWindow.SetActive(false);
            return;
        }

        // -----------------------------
        // Step 1: Merge into a single stack
        // -----------------------------
        // Step 1: Merge into a single stack
        List<SteamInventoryChecker.SteamStackInstance> currentStacks = SteamInventoryChecker.GetSteamItemInstances(itemDefId);

        if (currentStacks == null || currentStacks.Count == 0)
        {
            Debug.LogWarning($"No stack instances found for item {itemDefId}. Cannot make stacks.");
            makeStackWindow.SetActive(false);
            return;
        }

        SteamInventoryResult_t transferResult;
        SteamItemInstanceID_t mergedStack = currentStacks[0].instanceId;

        for (int i = 1; i < currentStacks.Count; i++)
        {
            SteamItemInstanceID_t sourceStack = currentStacks[i].instanceId;
            uint qtyToMove = (uint)currentStacks[i].quantity;

            bool mergeSuccess = SteamInventory.TransferItemQuantity(
                out transferResult,
                sourceStack,
                qtyToMove,
                mergedStack
            );

            if (mergeSuccess)
                SteamInventory.DestroyResult(transferResult);
            else
                Debug.LogWarning($"Failed to merge stack {i} for item {itemDefId}");
        }

        // -----------------------------
        // Step 2: Calculate desired stacks
        // -----------------------------
        int fullStacks = totalQty / newStackSize;
        int remainder = totalQty % newStackSize;

        List<int> desiredStacks = new List<int>();
        for (int i = 0; i < fullStacks; i++) desiredStacks.Add(newStackSize);
        if (remainder > 0) desiredStacks.Add(remainder);

        // -----------------------------
        // Step 3: Split from merged stack
        // -----------------------------
        int created = 0;
        foreach (int size in desiredStacks)
        {
            created += size;
            if (created == totalQty) break; // last stack is just the remainder

            bool splitSuccess = SteamInventory.TransferItemQuantity(
                out transferResult,
                mergedStack,
                (uint)size,
                SteamItemInstanceID_t.Invalid // split into new stack
            );

            if (splitSuccess)
                SteamInventory.DestroyResult(transferResult);
            else
                Debug.LogWarning($"Failed to split stack of size {size} for item {itemDefId}");
        }

        // -----------------------------
        // Step 4: Update local cache
        // -----------------------------
        InventoryManager.SetStacks(itemDefId, desiredStacks.ToArray());

        Debug.Log($"Split item {itemDefId} into stacks: {string.Join(",", desiredStacks)}");
        makeStackWindow.SetActive(false);

        success = true;
    }


    private void ConfirmStackAll()
    {
        int itemDefId = GameManager.currentItemDefToStack;

        // Get all Steam item instances for this itemDefId
        List<SteamInventoryChecker.SteamStackInstance> stackInstances = SteamInventoryChecker.GetSteamItemInstances(itemDefId);

        if (stackInstances.Count <= 1)
        {
            Debug.Log("Only one stack exists, nothing to merge.");
            makeStackWindow.SetActive(false);
            return;
        }

        SteamInventoryResult_t transferResult;

        // Merge all stacks into the first one
        SteamItemInstanceID_t firstStack = stackInstances[0].instanceId;

        for (int i = 1; i < stackInstances.Count; i++)
        {
            SteamItemInstanceID_t sourceStack = stackInstances[i].instanceId;
            uint quantityToMove = (uint)stackInstances[i].quantity;

            bool success = SteamInventory.TransferItemQuantity(
                out transferResult,
                sourceStack,
                quantityToMove,
                firstStack
            );

            if (success)
                SteamInventory.DestroyResult(transferResult);
            else
                Debug.LogWarning($"Failed to merge stack {i} for item {itemDefId}");
        }

        // Update local cache
        int totalQty = InventoryManager.GetQuantity(itemDefId);
        InventoryManager.SetStacks(itemDefId, new int[] { totalQty });

        Debug.Log($"All stacks merged into a single stack for item {itemDefId}");
        makeStackWindow.SetActive(false);

        success = true;


    }
    private bool success = false;

    private void ConfirmUnstackAll()
    {
        int itemDefId = GameManager.currentItemDefToStack;

        // Get all Steam item instances for this itemDefId
        List<SteamInventoryChecker.SteamStackInstance> stackInstances = SteamInventoryChecker.GetSteamItemInstances(itemDefId);

        if (stackInstances.Count == 0)
        {
            Debug.LogWarning("No stacks found to unstack.");
            makeStackWindow.SetActive(false);
            return;
        }

        SteamInventoryResult_t transferResult;

        // Split each existing stack into individual stacks of 1
        foreach (var stack in stackInstances)
        {
            SteamItemInstanceID_t sourceStack = stack.instanceId;
            int quantity = stack.quantity;

            for (int i = 1; i < quantity; i++) // leave one in the original
            {
                bool success = SteamInventory.TransferItemQuantity(
                    out transferResult,
                    sourceStack,
                    1,
                    SteamItemInstanceID_t.Invalid // split into a new stack
                );

                if (success)
                    SteamInventory.DestroyResult(transferResult);
                else
                    Debug.LogWarning($"Failed to split stack {sourceStack} for item {itemDefId}");
            }
        }

        // Update local cache: all stacks are size 1
        int totalQty = InventoryManager.GetQuantity(itemDefId);
        int[] newStacks = new int[totalQty];
        for (int i = 0; i < totalQty; i++) newStacks[i] = 1;
        InventoryManager.SetStacks(itemDefId, newStacks);

        Debug.Log($"Unstacked all items into stacks of 1 for item {itemDefId}");
        makeStackWindow.SetActive(false);

        success = true;

    }



}
