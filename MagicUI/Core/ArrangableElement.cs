﻿using MagicUI.Core.Internal;
using Modding;
using System;
using UnityEngine;

namespace MagicUI.Core
{
    /// <summary>
    /// Root class for arrangeable UI elements
    /// </summary>
    public abstract class ArrangableElement
    {
        private static readonly Loggable log = LogHelper.GetLogger();

        internal Rect PrevPlacementRect { get; private set; }

        public LayoutRoot LayoutRoot { get; private set; }

        /// <summary>
        /// Whether the most recent measurement is accurate
        /// </summary>
        public bool MeasureIsValid { get; private set; } = false;

        /// <summary>
        /// Whether the most recent arrangement is accurate
        /// </summary>
        public bool ArrangeIsValid { get; private set; } = false;

        /// <summary>
        /// The name of the arrangeable for lookup purposes
        /// </summary>
        public string Name { get; private set; }

        private HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left;
        /// <summary>
        /// The arrangeable's horizontal alignment
        /// </summary>
        public HorizontalAlignment HorizontalAlignment
        {
            get => horizontalAlignment;
            set
            {
                if (horizontalAlignment != value)
                {
                    horizontalAlignment = value;
                    InvalidateArrange();
                }
            }
        }

        private VerticalAlignment verticalAlignment = VerticalAlignment.Top;
        /// <summary>
        /// The arrangeable's vertical alignment
        /// </summary>
        public VerticalAlignment VerticalAlignment
        {
            get => verticalAlignment;
            set
            {
                if (verticalAlignment != value)
                {
                    verticalAlignment = value;
                    InvalidateArrange();
                }
            }
        }

        private Visibility visibility = Visibility.Visible;
        /// <summary>
        /// The actual visibility of the element
        /// </summary>
        public Visibility Visibility
        {
            get => visibility;
            set
            {
                if (visibility != value)
                {
                    // if we're going into or out of collapsed state, that may change our effective size
                    if (visibility == Visibility.Collapsed || value == Visibility.Collapsed)
                    {
                        InvalidateMeasure();
                    }
                    // if we're going into or out of fully visible state, that may affect our arrangement/rendering even if no size change
                    if (visibility == Visibility.Visible || value == Visibility.Visible)
                    {
                        InvalidateArrange();
                    }
                    visibility = value;
                }
            }
        }

        private Padding padding = Padding.Zero;
        public Padding Padding
        {
            get => padding;
            set
            {
                // note - default struct value comparison MAY be slow. I don't anticipate it's a huge issue though
                if (!padding.Equals(value))
                {
                    padding = value;
                    InvalidateMeasure();
                }
            }
        }

        public bool IsEffectivelyVisible
        {
            get
            {
                ArrangableElement? next = this;
                while (next != null)
                {
                    if (next.Visibility != Visibility) return false;
                    next = next.LogicalParent;
                }
                return true;
            }
        }

        /// <summary>
        /// The cached desired size. Set from the last result of <see cref="Measure"/>.
        /// </summary>
        public Vector2 DesiredSize { get; private set; }

        /// <summary>
        /// The cached effective size including padding and visibility. Set from the last result of <see cref="Measure"/>
        /// </summary>
        public Vector2 EffectiveSize { get; private set; }

        /// <summary>
        /// This element's parent in the layout hierarchy, if any
        /// </summary>
        public ArrangableElement? LogicalParent { get; protected internal set; } = null;

        public ArrangableElement(LayoutRoot onLayout, string name = "New ArrangeableElement")
        {
            Name = name;
            LayoutRoot = onLayout;
            onLayout.layoutOrchestrator.RegisterElement(this);
        }

        /// <summary>
        /// Indicates the measure is no longer valid; will trigger a full re-render of the visual tree.
        /// </summary>
        public void InvalidateMeasure()
        {
            MeasureIsValid = false;
            LogicalParent?.InvalidateMeasure();
        }

        /// <summary>
        /// Indicates the arrange is no longer valid; will trigger a rearrange of this element and its children.
        /// </summary>
        public void InvalidateArrange()
        {
            ArrangeIsValid = false;
        }

        /// <summary>
        /// Helper method to get the position of the top left corner during arrangement, given the component's vertical and horizontal alignments and padding
        /// </summary>
        private Vector2 GetAlignedTopLeftCorner(Rect availableSpace)
        {
            float x = horizontalAlignment switch
            {
                HorizontalAlignment.Left => availableSpace.xMin + Padding.Left,
                HorizontalAlignment.Center => availableSpace.xMin + availableSpace.width / 2 - DesiredSize.x / 2 + Padding.AddedWidth / 2,
                HorizontalAlignment.Right => availableSpace.xMax - DesiredSize.x - Padding.Right,
                _ => throw new NotImplementedException("Can't handle the current horizontal alignment"),
            };

            float y = verticalAlignment switch
            {
                VerticalAlignment.Top => availableSpace.yMin + Padding.Top,
                VerticalAlignment.Center => availableSpace.yMin + availableSpace.height / 2 - DesiredSize.y / 2 + Padding.AddedHeight / 2,
                VerticalAlignment.Bottom => availableSpace.yMax - DesiredSize.y - Padding.Bottom,
                _ => throw new NotImplementedException("Can't handle the current horizontal alignment"),
            };

            Vector2 vec = new(x, y);
            log.LogDebug($"{Name} top-left corner aligned and adjusted to {vec}");
            return vec;
        }

        /// <summary>
        /// Calculates the desired size of the object and caches it in <see cref="DesiredSize"/> for later reference in this UI build cycle.
        /// </summary>
        public Vector2 Measure()
        {
            if (!MeasureIsValid)
            {
                log.LogDebug($"Measure triggered for {Name}");
                DesiredSize = MeasureOverride();
                EffectiveSize = DesiredSize + new Vector2(Padding.AddedWidth, Padding.AddedHeight);
                if (Visibility == Visibility.Collapsed)
                {
                    EffectiveSize = Vector2.zero;
                }
                MeasureIsValid = true;
                InvalidateArrange();
                log.LogDebug($"Computed {Name} size as {EffectiveSize}, adjusted from {DesiredSize}");
            }
            return EffectiveSize;
        }

        /// <summary>
        /// Internal implementation to calculate desired size.
        /// </summary>
        protected abstract Vector2 MeasureOverride();

        /// <summary>
        /// Positions the object within the allocated space.
        /// </summary>
        /// <param name="availableSpace">The space available for the element.</param>
        public void Arrange(Rect availableSpace)
        {
            // only rearrange if we're either put into a new space or explicitly told to rearrange.
            if (!ArrangeIsValid || PrevPlacementRect != availableSpace)
            {
                log.LogDebug($"Arrange triggered for {Name} in {availableSpace}");
                ArrangeOverride(GetAlignedTopLeftCorner(availableSpace));
                PrevPlacementRect = availableSpace;
                ArrangeIsValid = true;
            }
        }

        /// <summary>
        /// Internal implementation to position the object within the allocated space.
        /// </summary>
        /// <param name="alignedTopLeftCorner">The space available for the element.</param>
        protected abstract void ArrangeOverride(Vector2 alignedTopLeftCorner);
    }
}
