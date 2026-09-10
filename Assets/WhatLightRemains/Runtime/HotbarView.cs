using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class HotbarView : MonoBehaviour
    {
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private GameObject slotTemplate;
        [SerializeField, Min(1)] private int slotCount = 8;
        [SerializeField] private Sprite toolIcon;
        [SerializeField, Min(0)] private int selectedIndex;

        private readonly List<GameObject> slots = new();

        public int SlotCount => slotCount;
        public int SelectedIndex => selectedIndex;
        public IReadOnlyList<GameObject> Slots => slots;

        public Sprite ToolIcon
        {
            get => toolIcon;
            set
            {
                toolIcon = value;
                RefreshSlots();
            }
        }

        public void Configure(RectTransform newSlotsRoot, GameObject newSlotTemplate, int newSlotCount = 8)
        {
            slotsRoot = newSlotsRoot;
            slotTemplate = newSlotTemplate;
            slotCount = Mathf.Max(1, newSlotCount);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, slotCount - 1);

            if (Application.isPlaying && isActiveAndEnabled)
            {
                Rebuild();
            }
        }

        public void Select(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, slotCount - 1));
            RefreshSlots();
        }

        public void SetToolIcon(Sprite icon)
        {
            ToolIcon = icon;
        }

        private void Awake()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].SetActive(false);
                    Destroy(slots[i]);
                }
            }

            slots.Clear();
            if (slotsRoot == null || slotTemplate == null)
            {
                return;
            }

            slotTemplate.SetActive(false);
            for (int i = 0; i < slotCount; i++)
            {
                GameObject slot = Instantiate(slotTemplate, slotsRoot, false);
                slot.name = $"Slot {i + 1}";
                slot.SetActive(true);
                slots.Add(slot);
            }

            RefreshSlots();
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                GameObject slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                Transform selection = FindDescendant(slot.transform, "Selection");
                if (selection != null)
                {
                    selection.gameObject.SetActive(i == selectedIndex);
                }
                else
                {
                    Outline outline = slot.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.enabled = i == selectedIndex;
                    }
                }

                Transform iconTransform = FindDescendant(slot.transform, "Icon");
                if (iconTransform != null && iconTransform.TryGetComponent(out Image iconImage))
                {
                    iconImage.sprite = i == 0 ? toolIcon : null;
                    iconImage.enabled = iconImage.sprite != null;
                }
            }
        }

        private static Transform FindDescendant(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            slotCount = Mathf.Max(1, slotCount);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, slotCount - 1);
        }
    }
}
