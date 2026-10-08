using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class WorldSpaceLayout : MonoBehaviour
{
    public enum LayoutDirection { X, Y, Z }

    [Title("Layout Configuration")]
    public LayoutDirection direction = LayoutDirection.X;
    public float spacing = 1.0f;
    public Vector3 offset = Vector3.zero;
    
    [Title("Settings")]
    public bool useBounds = true;
    public bool centerAlign = true;
    
    [Tooltip("Dành cho ForceRefresh cũ: Có tính luôn kích thước các mesh con hay không?")]
    public bool includeChildrenBounds = true;

    [Title("Custom Targets")]
    [Tooltip("Kéo thả MESH vào đây. Vị trí 0 đo cho Child 0, Vị trí 1 đo cho Child 1...")]
    public List<Transform> targetCustomer;
    
    [Button, PropertySpace(10)]
    public void ForceRefresh(bool useTween = false)
    {
        var children = GetActiveChildren();
        ApplyLayout(children, children, includeChildrenBounds, useTween);
    }
    
    [Button, PropertySpace(10)]
    public void ForceRefreshCustomList(List<Transform> customMeshes, bool useTween = false)
    {
        var children = GetActiveChildren();
        ApplyLayout(children, customMeshes, false, useTween);
    }

    [Button, PropertySpace(10)]
    public void ForceRefreshCustomListTarget(bool useTween = false)
    {
        var children = GetActiveChildren();
        ApplyLayout(children, targetCustomer, false, useTween);
    }

    private void ApplyLayout(List<Transform> objectsToMove, List<Transform> objectsToMeasure, bool calcChildren, bool useTween)
    {
        if (objectsToMove == null || objectsToMove.Count == 0) return;
        
        int count = objectsToMove.Count;
        var sizes = new float[count];
        var pivotOffsets = new Vector3[count];
        float totalSpan = 0;
        
        for (int i = 0; i < count; i++)
        {
            Transform root = objectsToMove[i];
            Transform measureTarget = (objectsToMeasure != null && i < objectsToMeasure.Count && objectsToMeasure[i] != null) 
                                    ? objectsToMeasure[i] 
                                    : root;

            if (useBounds)
            {
                Bounds b = GetBounds(measureTarget, calcChildren);
                sizes[i] = GetDimension(b.size);
                pivotOffsets[i] = root.position - b.center;
            }
            else
            {
                sizes[i] = 0;
                pivotOffsets[i] = Vector3.zero;
            }
            totalSpan += sizes[i];
        }
        
        totalSpan += (count - 1) * spacing;
        
        float currentPos = centerAlign ? -totalSpan / 2f : 0f;
        Vector3 baseWorldPos = transform.position + offset;
        
        for (int i = 0; i < count; i++)
        {
            Transform root = objectsToMove[i];
            float halfSize = sizes[i] / 2f;
            Vector3 targetPos = CalculatePosition(baseWorldPos, currentPos + halfSize) + pivotOffsets[i];

            if (useTween && Application.isPlaying)
                 root.DOMove(targetPos, 0.3f).SetEase(Ease.OutQuad);
            else
                 root.position = targetPos;

            currentPos += sizes[i] + spacing;
        }
    }

    private List<Transform> GetActiveChildren()
    {
        var result = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy)
            {
                result.Add(child);
            }
        }
        return result;
    }

    private float GetDimension(Vector3 size) => direction switch
    {
        LayoutDirection.X => size.x,
        LayoutDirection.Y => size.y,
        LayoutDirection.Z => size.z,
        _ => 0
    };

    private Vector3 CalculatePosition(Vector3 basePos, float moveDistance)
    {
        Vector3 result = basePos;
        if (direction == LayoutDirection.X) result.x += moveDistance;
        else if (direction == LayoutDirection.Y) result.y += moveDistance;
        else result.z += moveDistance;
        return result;
    }

    private Bounds GetBounds(Transform t, bool includeChildren)
    {
        Renderer[] renderers = includeChildren ? t.GetComponentsInChildren<Renderer>() : t.GetComponents<Renderer>();
        
        if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) 
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        return combined;
    }
}