using CodeStage.AntiCheat.Storage;
using Steamworks;
using UnityEngine;

public class SteamItemDropper : MonoBehaviour
{
    private Callback<SteamInventoryResultReady_t> dropCallback;
    private SteamInventoryResult_t dropResult;

    // Replace with your actual Playtime Generator ID
    private int playtimeGeneratorId = 1;
    [SerializeField] private bool dropOnStart = true;
    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam not initialized.");
            return;
        }
        if (dropOnStart)
        {

            TriggerSteamItemDropCheck();
        }
    }
    public void TriggerSteamItemDropCheck()
    {

        dropCallback = Callback<SteamInventoryResultReady_t>.Create(OnItemDropResult);

        // Trigger a drop from the generator
        bool success = SteamInventory.TriggerItemDrop(out dropResult, new SteamItemDef_t(playtimeGeneratorId));

        if (!success)
        {
            Debug.LogWarning("Failed to trigger item drop.");
        }
        else
        {
            Debug.Log("Item drop requested from playtime generator.");
        }
    }
    private void OnItemDropResult(SteamInventoryResultReady_t result)
    {
        if (result.m_handle != dropResult || result.m_result != EResult.k_EResultOK)
        {
            Debug.LogWarning("Item drop failed or result mismatched.");
            return;
        }

        uint itemCount = 0;
        if (!SteamInventory.GetResultItems(dropResult, null, ref itemCount))
        {
            Debug.LogWarning("Failed to get item count from drop.");
            return;
        }

        SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
        if (SteamInventory.GetResultItems(dropResult, items, ref itemCount))
        {
            foreach (var item in items)
            {
                Debug.Log($"Dropped item: {item.m_iDefinition}, quantity: {item.m_unQuantity}");
                // Optional: Save or use the item in your game here
                ObscuredPrefs.SetInt("Item" + item.m_iDefinition, 1);
                Debug.Log("Item ID: " + item.m_iDefinition + " dropped & unlocked");
            }
        }

        SteamInventory.DestroyResult(dropResult);
    }
}
