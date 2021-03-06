﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoxSelectionPlacementController : PlacementController<MapEvent, BeatmapEventContainer, EventsContainer>
{
    public static bool IsSelecting { get; private set; } = false;
    private Vector3 originPos;
    private float startTime;
    private RaycastHit previousHit;
    private Vector3 transformed;

    private HashSet<BeatmapObjectContainer> selected = new HashSet<BeatmapObjectContainer>();
    private HashSet<BeatmapObjectContainer> alreadySelected = new HashSet<BeatmapObjectContainer>();

    private List<BeatmapObject.Type> SelectedTypes = new List<BeatmapObject.Type>();

    [HideInInspector] protected override bool DestroyBoxCollider { get; set; } = false;
    [HideInInspector] protected override bool CanClickAndDrag { get; set; } = false;

    public override bool IsValid => (KeybindsController.CtrlHeld || IsSelecting) &&
        !(SongTimelineController.IsHovering || !IsActive) && Settings.Instance.BoxSelect;

    public override BeatmapAction GenerateAction(BeatmapEventContainer spawned, BeatmapObjectContainer conflicting) => null;

    public override MapEvent GenerateOriginalData() => null;

    public override void OnPhysicsRaycast(RaycastHit hit, Vector3 transformedPoint)
    {
        previousHit = hit;
        transformed = transformedPoint;
        CalculateTimes(hit, out _, out float realTime, out _, out _, out _);
        Vector3 position = hit.point;
        if (!IsSelecting)
        {
            SelectedTypes.Clear();
            if (hit.transform.GetComponentInParent<EventPlacement>()) SelectedTypes.Add(BeatmapObject.Type.EVENT);
            if (hit.transform.GetComponentInParent<NotePlacement>()) SelectedTypes.Add(BeatmapObject.Type.NOTE);
            if (hit.transform.GetComponentInParent<ObstaclePlacement>()) SelectedTypes.Add(BeatmapObject.Type.OBSTACLE);
            if (hit.transform.GetComponentInParent<CustomEventPlacement>()) SelectedTypes.Add(BeatmapObject.Type.CUSTOM_EVENT);
            instantiatedContainer.transform.localScale = Vector3.one;
            Vector3 localScale = instantiatedContainer.transform.localScale;
            startTime = realTime;
            position = new Vector3(position.x, position.y, (startTime - atsc.CurrentBeat) * EditorScaleController.EditorScale);
            instantiatedContainer.transform.position = position;
            instantiatedContainer.transform.localPosition -= new Vector3(localScale.x / 2, 0, localScale.z / 2);
        }
        else
        {
            instantiatedContainer.transform.localPosition = originPos;
            Vector3 instantiatedSpacePosition = instantiatedContainer.transform.parent.InverseTransformPoint(hit.point);
            Vector3 newLocalScale = instantiatedSpacePosition - originPos;
            newLocalScale = new Vector3(newLocalScale.x, Mathf.Max(newLocalScale.y, 1), newLocalScale.z);
            instantiatedContainer.transform.localScale = newLocalScale;

            selected.Clear();
            OverlapBox((containerBoye) =>
            {
                if (!alreadySelected.Contains(containerBoye) && !selected.Contains(containerBoye))
                {
                    selected.Add(containerBoye);
                    SelectionController.Select(containerBoye, true, false, false);
                }
            });
            SelectionController.RefreshSelectionMaterial(false);
            foreach (BeatmapObjectContainer combinedObj in new HashSet<BeatmapObjectContainer>(SelectionController.SelectedObjects))
            {
                if (!selected.Contains(combinedObj) && !alreadySelected.Contains(combinedObj))
                    SelectionController.Deselect(combinedObj);
            }
        }
    }

    public override void OnMousePositionUpdate(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsValid && IsSelecting)
            StartCoroutine(WaitABitFuckOffOtherPlacementControllers());
        base.OnMousePositionUpdate(context);
    }

    internal override void ApplyToMap()
    {
        if (!IsSelecting)
        {
            IsSelecting = true;
            originPos = instantiatedContainer.transform.localPosition;
            alreadySelected = new HashSet<BeatmapObjectContainer>(SelectionController.SelectedObjects);
        }
        else
        {
            StartCoroutine(WaitABitFuckOffOtherPlacementControllers());
            List<BeatmapObjectContainer> toSelect = new List<BeatmapObjectContainer>();

            OverlapBox((containerBoye) =>
            {
                if (containerBoye != null && SelectedTypes.Contains(containerBoye.objectData.beatmapType)) toSelect.Add(containerBoye);
            });

            foreach (BeatmapObjectContainer obj in toSelect) SelectionController.Select(obj, true, false);
            SelectionController.RefreshSelectionMaterial(toSelect.Any());
            IsSelecting = false;
            OnPhysicsRaycast(previousHit, transformed);
        }
    }

    private void OverlapBox(Action<BeatmapObjectContainer> action)
    {
        //Big brain boye does big brain things with big brain box
        BoxCollider boxyBoy = instantiatedContainer.GetComponent<BoxCollider>();
        Bounds bounds = new Bounds();
        bounds.center = boxyBoy.bounds.center;
        Vector3 absoluteLossyScale = new Vector3(
            Mathf.Abs(instantiatedContainer.transform.lossyScale.x),
            Mathf.Abs(instantiatedContainer.transform.lossyScale.y),
            Mathf.Abs(instantiatedContainer.transform.lossyScale.z)
            );
        bounds.size = absoluteLossyScale / 2f;
        Collider[] boxyBoyHitsStuffTM = Physics.OverlapBox(bounds.center, bounds.size, instantiatedContainer.transform.rotation, 1 << 9);
        foreach (Collider collider in boxyBoyHitsStuffTM)
        {
            BeatmapObjectContainer containerBoye = collider.gameObject.GetComponent<BeatmapObjectContainer>();
            if (!SelectedTypes.Contains(containerBoye.objectData.beatmapType)) continue;
            action?.Invoke(containerBoye);
        }
    }

    private IEnumerator WaitABitFuckOffOtherPlacementControllers()
    {
        yield return new WaitForSeconds(0.1f);
        IsSelecting = false;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || instantiatedContainer is null) return;
        Gizmos.color = Color.red;
        BoxCollider boxyBoy = instantiatedContainer.GetComponent<BoxCollider>();
        Bounds bounds = new Bounds();
        bounds.center = boxyBoy.bounds.center;
        bounds.size = instantiatedContainer.transform.lossyScale / 2f;
        Gizmos.DrawMesh(instantiatedContainer.GetComponentInChildren<MeshFilter>().mesh, bounds.center, instantiatedContainer.transform.rotation, bounds.size);
    }

    public override void TransferQueuedToDraggedObject(ref MapEvent dragged, MapEvent queued) { }
}
