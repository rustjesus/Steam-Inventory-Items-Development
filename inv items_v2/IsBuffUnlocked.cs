using CodeStage.AntiCheat.Storage;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro; // Required for IEnumerator

public class IsBuffUnlocked : MonoBehaviour
{
    public int itemNumber = 1000;
    [SerializeField] private TextMeshProUGUI countTxt;
    private Button thisButton;
    private InventoryDebugger inventoryDebugger;
    private Image thisImg;
    private SteamInventoryChecker inventoryChecker;
    private void Start()
    {
        thisImg = GetComponent<Image>();
        thisButton = GetComponent<Button>();
        InventoryDebugger.totalItemsCount++;

        inventoryChecker = FindAnyObjectByType<SteamInventoryChecker>();
        inventoryDebugger = FindAnyObjectByType<InventoryDebugger>();

        // Add click listener
        if (thisButton != null)
            thisButton.onClick.AddListener(OnButtonPressed);
        //StartCoroutine(DelayedAction());
    }


    private IEnumerator DelayedAction()
    {
        yield return new WaitForSeconds(1f);
        inventoryDebugger.RefreshStacks();
        inventoryChecker.SyncSteamItems();
    }
    private void Update()
    {
        if (ObscuredPrefs.GetInt("Item" + itemNumber.ToString()) >= 1) // drop buff 1
        {
            thisImg.color = Color.white;
        }
        if (ObscuredPrefs.GetInt("Item" + itemNumber.ToString()) <= 0) // drop buff 1
        {
            thisImg.color = Color.grey;
        }
        if (countTxt != null)
        {
            countTxt.text = ObscuredPrefs.GetInt("Item" + itemNumber.ToString()).ToString();
        }
        Debug.Log("Item" + itemNumber.ToString() + " | Qty: " + ObscuredPrefs.GetInt("Item" + itemNumber.ToString()).ToString());
    }
    private void OnButtonPressed()
    {
        InventoryDebugger.currentItemDefToStack = itemNumber;
        inventoryDebugger.stackWindow.SetActive(true);
        Debug.Log($"Selected item {itemNumber} for stacking.");
    }
}
