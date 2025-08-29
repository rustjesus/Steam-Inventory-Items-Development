using CodeStage.AntiCheat.Storage;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

public static class InventoryManager
{
    // item ID -> total quantity
    private static Dictionary<int, int> itemStacks = new Dictionary<int, int>();
    // item ID -> number of stacks in Steam inventory
    private static Dictionary<int, int> itemStackCounts = new Dictionary<int, int>();
    // item ID -> list of individual stack sizes
    private static Dictionary<int, List<int>> itemStackSizes = new Dictionary<int, List<int>>();

    public static Dictionary<int, int> GetAllStacks() => new Dictionary<int, int>(itemStacks);
    public static Dictionary<int, int> GetAllStackCounts() => new Dictionary<int, int>(itemStackCounts);
    public static Dictionary<int, List<int>> GetAllStackSizes() => new Dictionary<int, List<int>>(itemStackSizes);

    /// <summary>
    /// Sync items from Steam inventory into cache and ObscuredPrefs.
    /// Stores stack sizes for each item.
    /// </summary>
    public static void SyncFromSteam(SteamItemDetails_t[] items)
    {
        itemStacks.Clear();
        itemStackCounts.Clear();
        itemStackSizes.Clear();

        foreach (var item in items)
        {
            int defId = (int)item.m_iDefinition;
            int quantity = (int)item.m_unQuantity;

            // total quantity
            if (itemStacks.ContainsKey(defId))
                itemStacks[defId] += quantity;
            else
                itemStacks[defId] = quantity;

            // count stacks
            if (itemStackCounts.ContainsKey(defId))
                itemStackCounts[defId] += 1;
            else
                itemStackCounts[defId] = 1;

            // add stack size
            if (!itemStackSizes.ContainsKey(defId))
                itemStackSizes[defId] = new List<int>();
            itemStackSizes[defId].Add(quantity);

            // persist total quantity
            ObscuredPrefs.SetInt("Item" + defId, itemStacks[defId]);
        }

        Debug.Log("Inventory synced. Total items: " + itemStacks.Count);
    }

    /// <summary>
    /// Get total quantity of an item.
    /// </summary>
    public static int GetQuantity(int defId)
    {
        if (itemStacks.TryGetValue(defId, out int qty))
            return qty;
        return ObscuredPrefs.GetInt("Item" + defId, 0);
    }

    /// <summary>
    /// Get number of stacks for an item.
    /// </summary>
    public static int GetStackCount(int defId)
    {
        if (itemStackCounts.TryGetValue(defId, out int count))
            return count;
        return 0;
    }

    /// <summary>
    /// Get array of stack sizes for an item.
    /// </summary>
    public static int[] GetStackSizes(int defId)
    {
        if (itemStackSizes.TryGetValue(defId, out List<int> sizes))
            return sizes.ToArray();
        return new int[0];
    }
    public static void SetStacks(int itemId, int[] newStackSizes)
    {
        itemStackSizes[itemId] = new List<int>(newStackSizes);

        // update total quantity
        int totalQty = 0;
        foreach (var qty in newStackSizes)
            totalQty += qty;
        itemStacks[itemId] = totalQty;

        // update stack count
        itemStackCounts[itemId] = newStackSizes.Length;

        // persist total quantity
        ObscuredPrefs.SetInt("Item" + itemId, totalQty);
    }
    public static void ApplyStacksToSteam(int itemId, int[] stackSizes)
    {
        if (!SteamManager.Initialized) return;

        // Get current Steam stacks: returns quantities
        var steamStacks = SteamInventoryChecker.GetSteamItemStacks(itemId);

        // For proper consume, get instance IDs of each stack
        var steamInstances = SteamInventoryChecker.GetSteamItemInstances(itemId);

        int totalOld = 0;
        foreach (var qty in steamStacks) totalOld += qty;
        int totalNew = 0;
        foreach (var qty in stackSizes) totalNew += qty;

        if (totalNew > totalOld)
        {
            int addAmount = totalNew - totalOld;
            // Grant new items
            for (int i = 0; i < addAmount; i++)
                SteamInventoryChecker.GrantSteamItem(itemId);
        }
        else if (totalNew < totalOld)
        {
            int removeAmount = totalOld - totalNew;

            // Consume from stacks one by one until removed amount is reached
            int toRemove = removeAmount;
            foreach (var instance in steamInstances)
            {
                if (toRemove <= 0) break;
                int consumeQty = Mathf.Min(instance.quantity, toRemove);
                SteamInventoryChecker.ConsumeSteamItem(instance.instanceId, consumeQty);
                toRemove -= consumeQty;
            }
        }

        // Update local cache
        SetStacks(itemId, stackSizes);
    }


}
