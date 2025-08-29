using CodeStage.AntiCheat.Storage;
using SecureVariables;
using TMPro;
using UnityEngine;
using System.Collections;

public class ItemDropperLoop : MonoBehaviour
{
    private SteamItemDropper steamItemDropper;
    [SerializeField] private TextMeshProUGUI dropBuffText;

    private SecureFloat loopTime = 900f;// 15 minutes
    private void Awake()
    {
        steamItemDropper = GetComponent<SteamItemDropper>();
    }

    private void Start()
    {
        StartCoroutine(DelayedAction());
    }
    IEnumerator DelayedAction()
    {
        yield return new WaitForSeconds(1f);

        if (ObscuredPrefs.GetInt("Item1000") >= 1)//drop buff 1
        {
            loopTime = loopTime - 120f;// -2 minutes
        }
        if (ObscuredPrefs.GetInt("Item2000") >= 1)//drop buff 2
        {
            loopTime = loopTime - 120f;// -2 minutes
        }
        if (ObscuredPrefs.GetInt("Item3000") >= 1)//drop buff 3
        {
            loopTime = loopTime - 120f;// -2 minutes
        }
        if (ObscuredPrefs.GetInt("Item4000") >= 1)//drop buff 4
        {
            loopTime = loopTime - 240f;// -4 minutes
        }

        StartCoroutine(DropCheckLoop(loopTime));

        dropBuffText.text = "~" + loopTime + "s";
    }
    private IEnumerator DropCheckLoop(SecureFloat loopTime)
    {

        yield return new WaitForSeconds(loopTime); 
        steamItemDropper.TriggerSteamItemDropCheck();
        Debug.Log("Steam item drop loop checking...");
        StartCoroutine(DropCheckLoop(loopTime));
    }
}
