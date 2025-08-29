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

    private void Awake()
    {
        inventoryDebugger = FindAnyObjectByType<InventoryDebugger>();
        thisButton = GetComponent<Button>();
        GameManager.totalItemsCount++;

        // Add click listener
        if (thisButton != null)
            thisButton.onClick.AddListener(OnButtonPressed);
    }

    private void Start()
    {
        StartCoroutine(DelayedAction());
    }

    private IEnumerator DelayedAction()
    {
        yield return new WaitForSeconds(0.2f);

        if (ObscuredPrefs.GetInt("Item" + itemNumber.ToString()) >= 1) // drop buff 1
        {
            GetComponent<Image>().color = Color.white;
        }

        if (countTxt != null)
        {
            countTxt.text = ObscuredPrefs.GetInt("Item" + itemNumber.ToString()).ToString();
        }
    }

    private void OnButtonPressed()
    {
        GameManager.currentItemDefToStack = itemNumber;
        inventoryDebugger.stackWindow.SetActive(true);
        Debug.Log($"Selected item {itemNumber} for stacking.");
    }
}
