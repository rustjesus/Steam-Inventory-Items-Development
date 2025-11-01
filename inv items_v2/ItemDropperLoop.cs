using CodeStage.AntiCheat.Storage;
using SecureVariables;
using TMPro;
using UnityEngine;
using System.Collections;

public class ItemDropperLoop : MonoBehaviour
{
    private SteamItemDropper steamItemDropper;
    [SerializeField] private TextMeshProUGUI dropBuffText;

    private SecureFloat loopTime = 60f;// 1 min
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

        StartCoroutine(DropCheckLoop(loopTime));

        if(dropBuffText != null)
        {

            dropBuffText.text = "~" + loopTime + "s";
        }
    }
    private IEnumerator DropCheckLoop(SecureFloat loopTime)
    {

        yield return new WaitForSeconds(loopTime); 
        steamItemDropper.TriggerSteamItemDropCheck();
        Debug.Log("Steam item drop loop checking...");
        StartCoroutine(DropCheckLoop(loopTime));
    }
}
