using CodeStage.AntiCheat.Storage;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;


public class SteamInventoryChecker : MonoBehaviour
{
    private Callback<SteamInventoryResultReady_t> inventoryCallback;
    private SteamInventoryResult_t inventoryResult;

    // Replace with your actual Item Definition IDs
    //current build setting, 6 items max
    //for update
    private int[] targetItemDefIds = { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,
        25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,
        51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,
        81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,
        100, 200, 300, 400, 500, 600, 700, 800, 900, 1000,
        1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000, 2100, 2200, 2300, 2400,
        2500, 2600, 2700, 2800, 2900, 3000, 3100, 3200, 3300, 3400, 3500, 3600, 3700, 3800,
        3900, 4000};
    //100 blue hat, 200 red hat, 1001 gold coin
    private HashSet<int> foundItemDefs = new HashSet<int>();
    public static bool oncePerSession = false;

    private void Start()
    {
        //reset all item unlocks to ensure they are always 0 on init
        for (int i = 0; i < targetItemDefIds.Length; i++)
        {
            ObscuredPrefs.SetInt("Item" + targetItemDefIds[i], 0);
        }


        if (oncePerSession == false)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("Steam not initialized.");
                return;
            }

            oncePerSession = true;
        }

        inventoryCallback = Callback<SteamInventoryResultReady_t>.Create(OnSteamInventoryResultReady);

        if (!SteamInventory.GetAllItems(out inventoryResult))
        {
            Debug.LogError("Failed to request inventory.");
        }
    }


    public void SyncSteamItems()
    {
        uint itemCount = 0;
        SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
        if (SteamInventory.GetResultItems(inventoryResult, items, ref itemCount))
        {
            // Sync stacks into ObscuredPrefs
            InventoryManager.SyncFromSteam(items);
        }
        SteamInventory.DestroyResult(inventoryResult);
    }
    private void OnSteamInventoryResultReady(SteamInventoryResultReady_t callback)
    {
        if (callback.m_handle != inventoryResult || callback.m_result != EResult.k_EResultOK)
        {
            Debug.LogWarning("Inventory result failed or mismatched.");
            return;
        }

        uint itemCount = 0;
        if (!SteamInventory.GetResultItems(inventoryResult, null, ref itemCount))
            return;

        SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
        if (SteamInventory.GetResultItems(inventoryResult, items, ref itemCount))
        {
            // Sync stacks into ObscuredPrefs
            InventoryManager.SyncFromSteam(items);
        }

        SteamInventory.DestroyResult(inventoryResult);
    }
    /// <summary>
    /// Represents a single Steam item stack instance.
    /// </summary>
    public struct SteamStackInstance
    {
        public SteamItemInstanceID_t instanceId;
        public int quantity;

        public SteamStackInstance(SteamItemInstanceID_t id, int qty)
        {
            instanceId = id;
            quantity = qty;
        }
    }

    /// <summary>
    /// Get all instances (stacks) of a specific item in Steam inventory,
    /// including their instance IDs and quantities.
    /// </summary>
    public static List<SteamStackInstance> GetSteamItemInstances(int itemDefId)
    {
        var resultList = new List<SteamStackInstance>();
        if (!SteamManager.Initialized) return resultList;

        if (!SteamInventory.GetAllItems(out SteamInventoryResult_t result))
            return resultList;

        uint count = 0;
        SteamInventory.GetResultItems(result, null, ref count);
        SteamItemDetails_t[] items = new SteamItemDetails_t[count];
        SteamInventory.GetResultItems(result, items, ref count);

        foreach (var item in items)
        {
            if ((int)item.m_iDefinition == itemDefId)
            {
                resultList.Add(new SteamStackInstance(item.m_itemId, (int)item.m_unQuantity));
            }
        }

        SteamInventory.DestroyResult(result);
        return resultList;
    }

    // ---------------- Steam Inventory Functions ----------------

    /// <summary>
    /// Get all stacks for a specific item in Steam inventory.
    /// </summary>
    public static List<int> GetSteamItemStacks(int itemDefId)
    {
        var stacks = new List<int>();
        if (!SteamManager.Initialized) return stacks;

        if (!SteamInventory.GetAllItems(out SteamInventoryResult_t result))
            return stacks;

        uint count = 0;
        SteamInventory.GetResultItems(result, null, ref count);
        SteamItemDetails_t[] items = new SteamItemDetails_t[count];
        SteamInventory.GetResultItems(result, items, ref count);

        foreach (var item in items)
            if ((int)item.m_iDefinition == itemDefId)
                stacks.Add((int)item.m_unQuantity);

        SteamInventory.DestroyResult(result);
        return stacks;
    }


    /// <summary>
    /// Consume a specific amount of an item in Steam inventory.
    /// </summary>
    /// <summary>
    /// Consume a specific amount of an item in Steam inventory.
    /// </summary>
    public static bool ConsumeSteamItem(SteamItemInstanceID_t instanceId, int amount)
    {
        if (!SteamManager.Initialized || amount <= 0) return false;

        SteamInventoryResult_t consumeResult;
        bool success = SteamInventory.ConsumeItem(out consumeResult, instanceId, (uint)amount);

        if (!success)
        {
            Debug.LogWarning("Failed to consume Steam item: " + instanceId);
            return false;
        }

        // Always destroy the result when done
        SteamInventory.DestroyResult(consumeResult);
        return true;
    }

    /// <summary>
    /// Grant a single promotional item to the Steam inventory.
    /// </summary>
    public static bool GrantSteamItem(int itemDefId)
    {
        if (!SteamManager.Initialized) return false;

        SteamInventoryResult_t grantResult;
        bool success = SteamInventory.AddPromoItem(out grantResult, (SteamItemDef_t)itemDefId);

        if (!success)
        {
            Debug.LogWarning("Failed to grant Steam item: " + itemDefId);
            return false;
        }

        // Always destroy the result when done
        SteamInventory.DestroyResult(grantResult);
        return true;
    }



}
