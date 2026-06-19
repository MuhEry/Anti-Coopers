using Unity.Netcode;
using UnityEngine;

public class CrumblingTile : MonoBehaviour
{
    [SerializeField] private float delayBeforeCrumble = 1.3f;
    public float DelayBeforeCrumble => delayBeforeCrumble;

    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;
    private MaterialPropertyBlock propBlock;
    private bool isSteppedOn = false;
    private int myIndex;
    private TileManager manager;

    public void Init(int index, TileManager mgr)
    {
        myIndex = index;
        manager = mgr;
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider>();
        propBlock = new MaterialPropertyBlock();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (isSteppedOn) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            isSteppedOn = true;
            manager.RequestCrumble(myIndex);
        }
    }

    public void ShowWarning()
    {
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", Color.red); // URP rengi
        meshRenderer.SetPropertyBlock(propBlock);
    }

    public void Hide()
    {
        meshRenderer.enabled = false;
        boxCollider.enabled = false;
    }

    public void ResetLocal()
    {
        isSteppedOn = false;
        meshRenderer.enabled = true;
        boxCollider.enabled = true;
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", Color.white);
        meshRenderer.SetPropertyBlock(propBlock);
    }
}