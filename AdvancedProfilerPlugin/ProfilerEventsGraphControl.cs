using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AdvancedProfiler;

class ProfilerEventsGraphControl : Control
{
    static ProfilerEventsGraphControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ProfilerEventsGraphControl), new FrameworkPropertyMetadata(typeof(ProfilerEventsGraphControl)));
    }

    SolidColorBrush headerBrush;
    SolidColorBrush backgroundBrush;
    Pen intervalLinePen;
    SolidColorBrush eventBrush;
    SolidColorBrush selectionBrush;
    Pen selectionPen;
    SolidColorBrush hoverInfoBackgroundBrush;
    Typeface fontFace;

    const int headerHeight = 20;
    const float minBarWidth = 3;
    const float barHeight = 18;
    const float minBarHeight = 3;
    const float threadGroupPadding = 6;

    const double maxZoom = 1000 * 50; // 50px per µs

    long startTime;
    long endTime;

    double minZoom;

    public double Zoom
    {
        get => zoom;
        set
        {
            zoom = value;
            ContentWidth = (int)TicksToPixels(endTime - startTime);
        }
    }
    double zoom = 1;

    long shiftX;

    public int ContentWidth
    {
        get => contentWidth;
        private set
        {
            contentWidth = value;
            UpdateHScroll();
        }
    }
    int contentWidth;

    public int ContentHeight
    {
        get => contentHeight;
        private set
        {
            contentHeight = value;
            UpdateVScroll();
        }
    }
    int contentHeight;

    double ViewportWidth => Math.Max(0, ActualWidth - vScroll.Width);
    double ViewportHeight => Math.Max(0, ActualHeight - hScroll.Height);

    Point mousePos;
    (int SegmentIndex, int EventIndex) hoverIndices = (-1, -1);
    Point mouseDownPos;
    bool zoomSelectionStarted;

    bool wasRecording;

    StringBuilder stringBuilder = new();
    StringBuilder stringBuilder2 = new();
    StringBuilder stringBuilder3 = new();

    Dictionary<Vector3, SolidColorBrush> solidBrushes = [];

    List<(int Segment, int Index)> hoverEvents = [];
    (float StartX, float FillPercent)[] minifiedDrawStack = [];

    ScrollBar hScroll;
    ScrollBar vScroll;

    VisualCollection visualChildren;
    DrawingVisual backgroundDrawing;
    DrawingVisual graphDrawing;
    DrawingVisual selectionDrawing;
    DrawingVisual hoverDrawing;

    public ProfilerEventsGraphControl()
    {
        backgroundBrush = new SolidColorBrush(new Color { R = 50, G = 50, B = 50, A = 255 });
        headerBrush = new SolidColorBrush(new Color { R = 20, G = 20, B = 20, A = 255 });
        intervalLinePen = new Pen(new SolidColorBrush(new Color { R = 80, G = 80, B = 80, A = 255 }), 1);
        eventBrush = new SolidColorBrush(Colors.Red);
        selectionBrush = new SolidColorBrush(NewColor(0, 0, 0, 100));
        selectionPen = new Pen(new SolidColorBrush(NewColor(150, 150, 150)), 2);
        hoverInfoBackgroundBrush = new SolidColorBrush(new Color { A = 190 });
        fontFace = FontFamily.GetTypefaces().First();
        FontSize = 14;

        hScroll = new ScrollBar { Orientation = Orientation.Horizontal };
        vScroll = new ScrollBar { Orientation = Orientation.Vertical };

        hScroll.Scroll += HScroll_Scroll;
        vScroll.Scroll += VScroll_Scroll;

        backgroundDrawing = new DrawingVisual();
        graphDrawing = new DrawingVisual();
        selectionDrawing = new DrawingVisual();
        hoverDrawing = new DrawingVisual { Transform = new TranslateTransform() };

        visualChildren = new VisualCollection(this) {
            backgroundDrawing, // Always need something to hit test against for mouse events
            graphDrawing,
            selectionDrawing,
            hoverDrawing,
            hScroll,
            vScroll
        };
    }

    void UpdateHScroll()
    {
        hScroll.Maximum = contentWidth - ViewportWidth;
        hScroll.IsEnabled = contentWidth > ViewportWidth;
    }

    void UpdateVScroll()
    {
        double vh = ViewportHeight - headerHeight;

        vScroll.Maximum = contentHeight - vh;
        vScroll.IsEnabled = contentHeight > vh;
    }

    void HScroll_Scroll(object sender, ScrollEventArgs args)
    {
        shiftX = -(long)PixelsToTicks(args.NewValue);

        InvalidateVisual();
    }

    void VScroll_Scroll(object sender, ScrollEventArgs args)
    {
        InvalidateVisual();
    }

    protected override int VisualChildrenCount => visualChildren.Count;
    protected override Visual GetVisualChild(int index) => visualChildren[index];

    protected override Size MeasureOverride(Size constraint)
    {
        int visualChildrenCount = VisualChildrenCount;

        if (visualChildrenCount > 0)
        {
            var uIElement = GetVisualChild(0) as UIElement;

            if (uIElement != null)
            {
                uIElement.Measure(constraint);
                return uIElement.DesiredSize;
            }
        }

        return new Size(0.0, 0.0);
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        hScroll.Arrange(new Rect(0, arrangeBounds.Height - hScroll.Height, arrangeBounds.Width - vScroll.Width, hScroll.Height));
        vScroll.Arrange(new Rect(arrangeBounds.Width - vScroll.Width, 0, vScroll.Width, arrangeBounds.Height - hScroll.Height));

        return arrangeBounds;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "FontFamily")
            fontFace = FontFamily.GetTypefaces().First();

        base.OnPropertyChanged(e);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (sizeInfo.WidthChanged)
        {
            hScroll.ViewportSize = ViewportWidth;
            UpdateHScroll();
        }

        if (sizeInfo.HeightChanged)
        {
            vScroll.ViewportSize = Math.Max(0, ViewportHeight - headerHeight);
            UpdateVScroll();
        }
    }

    protected override void OnMouseMove(MouseEventArgs args)
    {
        mousePos = args.GetPosition(this);

        base.OnMouseMove(args);

        if (zoomSelectionStarted)
            RedrawSelection(mousePos);

        if (Profiler.IsRecordingEvents)
            return;

        var threadProfilers = Profiler.GetProfilerGroups();
        Array.Sort(threadProfilers, ThreadGroupComparer);

        ProfilerGroup? hoverGroup = null;
        float hoverY = 0;
        float y = headerHeight - (float)vScroll.Value;
        bool reDraw = false;

        for (int i = 0; i < threadProfilers.Length; i++)
        {
            int maxDepth = 0;

            GetHoveredEvents(threadProfilers[i], startTime, y, ref maxDepth);

            if (hoverEvents.Count > 0)
            {
                var (segmentIndex, eventIndex) = hoverEvents[0];

                if (segmentIndex != hoverIndices.SegmentIndex || eventIndex != hoverIndices.EventIndex)
                    reDraw = true;

                hoverGroup = threadProfilers[i];
                hoverIndices = (segmentIndex, eventIndex);
                hoverY = y;
                break;
            }

            y += barHeight * maxDepth + threadGroupPadding;
        }

        if (hoverGroup != null)
        {
            if (reDraw)
            {
                var ctx = hoverDrawing.RenderOpen();
                DrawHoverInfo(ctx, hoverGroup, x: 0, y: 0);
                ctx.Close();
            }
            else
            {
                hoverEvents.Clear();
            }

            double startX = mousePos.X + 16; // 16 is Mouse cursor offset fudge
            double width = hoverDrawing.Drawing.Bounds.Width;

            if (startX + width > ViewportWidth)
                startX = ViewportWidth - width;

            var tt = (TranslateTransform)hoverDrawing.Transform;
            tt.X = startX;
            tt.Y = hoverY;
        }
        else
        {
            hoverIndices = (-1, -1);
            hoverDrawing.RenderOpen().Close(); // Clear
        }
    }

    void GetHoveredEvents(ProfilerGroup group, long startTicks, float startY, ref int maxDepth)
    {
        if (group.NextEventIndex == 0)
            return;

        double graphWidth = ViewportWidth;
        int endSegmentIndex = (group.NextEventIndex - 1) / ProfilerGroup.EventBufferSegmentSize;

        for (int i = 0; i <= endSegmentIndex; i++)
        {
            var segment = group.Events[i];
            int endEventIndex = Math.Min(segment.Length, group.NextEventIndex - i * ProfilerGroup.EventBufferSegmentSize) - 1;

            if (segment[endEventIndex].EndTime - startTicks < -shiftX)
                continue;

            if (segment[0].StartTime - startTicks > -shiftX + (long)PixelsToTicks(graphWidth))
                break;

            for (int j = 0; j <= endEventIndex; j++)
            {
                ref var _event = ref segment[j];

                if (_event.Depth + 1 > maxDepth)
                    maxDepth = _event.Depth + 1;

                float startX = (float)TicksToPixels(_event.StartTime - startTicks + shiftX);
                float width = _event.IsSinglePoint ? 4 : (float)TicksToPixels(_event.EndTime - _event.StartTime);

                if (startX + width < 0 || startX > graphWidth)
                    continue;

                float barY = startY + _event.Depth * barHeight;
                double floorX = Math.Floor(startX);

                if (mousePos.X >= floorX && mousePos.X < floorX + Math.Max(minBarWidth, width)
                    && mousePos.Y >= barY && mousePos.Y < barY + barHeight)
                {
                    hoverEvents.Add((i, j));
                }
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // Start selection
            mouseDownPos = args.GetPosition(this);
            zoomSelectionStarted = true;

            RedrawSelection(mouseDownPos);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);

        if (zoomSelectionStarted)
        {
            // End selection
            var pos = args.GetPosition(this);
            double startX = Math.Min(mouseDownPos.X, pos.X);
            double endX = Math.Max(mouseDownPos.X, pos.X);

            long start = (long)PixelsToTicks(startX);
            long end = (long)PixelsToTicks(endX);

            shiftX -= start;

            double zoom = ViewportWidth / ((end - start) / (double)TimeSpan.TicksPerMillisecond);
            Zoom = Math.Min(zoom, maxZoom);

            hScroll.Value = TicksToPixels(-shiftX);

            zoomSelectionStarted = false;
            selectionDrawing.RenderOpen().Close(); // Clear

            InvalidateVisual(); // Redraw graph
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        if (zoomSelectionStarted)
        {
            zoomSelectionStarted = false;
            selectionDrawing.RenderOpen().Close(); // Clear
        }
    }

    protected override void OnMouseLeave(MouseEventArgs args)
    {
        base.OnMouseLeave(args);

        if (zoomSelectionStarted)
        {
            zoomSelectionStarted = false;
            selectionDrawing.RenderOpen().Close(); // Clear
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            double oldZoom = zoom;
            double delta = args.Delta * ((1.0 / 100) * 0.05) * zoom;

            Zoom = Math.Min(Math.Max(zoom + delta, minZoom), maxZoom);

            var pos = args.GetPosition(this);

            double oldOffset = pos.X / (oldZoom / TimeSpan.TicksPerMillisecond);
            double newOffset = pos.X / (zoom / TimeSpan.TicksPerMillisecond);

            shiftX += (long)(newOffset - oldOffset);

            if (shiftX > 0)
                shiftX = 0;

            hScroll.Value = TicksToPixels(-shiftX);
        }
        else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            double scale = zoom / TimeSpan.TicksPerMillisecond;
            shiftX += (long)((args.Delta * ((1.0 / 120) * 55)) / scale);

            if (shiftX > 0)
                shiftX = 0;

            hScroll.Value = TicksToPixels(-shiftX);
        }
        else if (args.GetPosition(this).X < ViewportWidth)
        {
            vScroll.Value -= args.Delta;

            // TODO: Don't redraw, translate visual instead
        }

        InvalidateVisual(); // Redraw graph
    }

    void RedrawSelection(Point mousePos)
    {
        var ctx = selectionDrawing.RenderOpen();

        double graphWidth = ViewportWidth;
        double graphHeight = ViewportHeight;

        ctx.PushClip(new RectangleGeometry(new Rect(0, 0, graphWidth, graphHeight)));

        double startX = Math.Min(mouseDownPos.X, mousePos.X);
        double endX = Math.Max(mouseDownPos.X, mousePos.X);

        var rect = new Rect(startX, 0, endX - startX, graphHeight);
        ctx.DrawRectangle(selectionBrush, selectionPen, rect);

        ctx.Close();
    }

    public void ResetZoom()
    {
        ResetZoomInternal();
        InvalidateVisual();
    }

    double PixelsToTicks(double pixels) => pixels / zoom * TimeSpan.TicksPerMillisecond;
    double TicksToPixels(long ticks) => ticks / (double)TimeSpan.TicksPerMillisecond * zoom;

    void ResetZoomInternal()
    {
        if (startTime == long.MaxValue)
            Zoom = minZoom = 1;
        else
            Zoom = minZoom = ViewportWidth / ((endTime - startTime) / (double)TimeSpan.TicksPerMillisecond);

        shiftX = 0;
        hScroll.Value = 0;
    }

    void UpdateValues()
    {
        Profiler.Start();

        var threadProfilers = Profiler.GetProfilerGroups();

        startTime = long.MaxValue;
        endTime = 0;

        for (int i = 0; i < threadProfilers.Length; i++)
            GetEventTimeBounds(threadProfilers[i], ref startTime, ref endTime);

        float y = 0;

        for (int i = 0; i < threadProfilers.Length; i++)
        {
            int maxDepth = GetMaxDepthForGroup(threadProfilers[i]);
            y += barHeight * maxDepth + threadGroupPadding;
        }

        ContentHeight = (int)y;
        ResetZoomInternal();

        hoverIndices = (-1, -1);
        hoverDrawing.RenderOpen().Close(); // Clear

        InvalidateVisual();

        Profiler.Stop();
    }

    static void GetEventTimeBounds(ProfilerGroup group, ref long startTime, ref long endTime)
    {
        if (group.NextEventIndex == 0)
            return;

        long start = group.Events[0][0].StartTime;

        if (start < startTime)
            startTime = start;

        const int ss = ProfilerGroup.EventBufferSegmentSize;

        int lastIndex = group.NextEventIndex - 1;
        var endSegment = group.Events[lastIndex / ss];
        long end = endSegment[lastIndex % ss].EndTime;

        if (end > endTime)
            endTime = end;
    }

    static int GetMaxDepthForGroup(ProfilerGroup group)
    {
        if (group.NextEventIndex == 0)
            return 0;

        int maxDepth = 0;
        int endSegmentIndex = (group.NextEventIndex - 1) / ProfilerGroup.EventBufferSegmentSize;

        for (int i = 0; i <= endSegmentIndex; i++)
        {
            var segment = group.Events[i];
            int endEventIndex = Math.Min(segment.Length, group.NextEventIndex - i * ProfilerGroup.EventBufferSegmentSize);

            for (int j = 0; j < endEventIndex; j++)
            {
                ref var _event = ref segment[j];

                if (_event.Depth + 1 > maxDepth)
                    maxDepth = _event.Depth + 1;
            }
        }

        return maxDepth;
    }

    static int ThreadGroupComparer(ProfilerGroup a, ProfilerGroup b)
    {
        int order = 0;

        if (a.SortingGroup != null)
        {
            if (b.SortingGroup != null)
                order = Profiler.CompareSortingGroups(a.SortingGroup, b.SortingGroup);
            else
                order = -1;
        }
        else if (b.SortingGroup != null)
        {
            order = 1;
        }

        if (order == 0)
        {
            order = a.OrderInSortingGroup.CompareTo(b.OrderInSortingGroup);

            if (order == 0)
                order = string.Compare(a.Name, b.Name);
        }

        return order;
    }

    protected override void OnRender(DrawingContext drawCtx)
    {
        Profiler.Start("Draw ProfilerEventsGraph");

        base.OnRender(drawCtx);

        if ((wasRecording || contentHeight == 0) && !Profiler.IsRecordingEvents)
            UpdateValues();

        wasRecording = Profiler.IsRecordingEvents;

        double graphWidth = ViewportWidth;
        double graphHeight = ViewportHeight;

        if (graphWidth == 0 || graphHeight < headerHeight)
        {
            Profiler.Stop();
            return;
        }

        var bgdCtx = backgroundDrawing.RenderOpen();

        bgdCtx.DrawRectangle(headerBrush, null, new Rect(0, 0, graphWidth, headerHeight));
        bgdCtx.DrawRectangle(backgroundBrush, null, new Rect(0, headerHeight, graphWidth, graphHeight - headerHeight));

        bgdCtx.Close();

        var graphCtx = graphDrawing.RenderOpen();

        graphCtx.PushClip(new RectangleGeometry(new Rect(0, 0, graphWidth, graphHeight)));

        // Draw time intervals
        {
            double lineInterval = 128.0 / Math.Pow(2, Math.Floor(Math.Log(zoom, 2)));
            lineInterval = Math.Max(lineInterval, 0.001);
            double left = -shiftX / (double)TimeSpan.TicksPerMillisecond;
            double offset = left % lineInterval;
            double x = (lineInterval - offset) * zoom;
            int lineCount = (int)(graphWidth / (lineInterval * zoom) + x);

            double unitScale = 1;
            string unit = "ms";

            if (lineInterval < 1)
            {
                unitScale = 1000;
                unit = "µs";
            }

            var sb = stringBuilder;

            for (int i = 0; i < lineCount; i++)
            {
                int x2 = (int)(x + i * lineInterval * zoom);
                graphCtx.DrawLine(intervalLinePen, new Point(x2, 16), new Point(x2, graphHeight));

                double time = (left - offset + (1 + i) * lineInterval) * unitScale;
                sb.AppendFormat("{0:N0}", time).Append(unit);

                var ft = new FormattedText(sb.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    fontFace, FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip) { TextAlignment = TextAlignment.Center };

                graphCtx.DrawText(ft, new Point(x2, 0));

                sb.Clear();
            }
        }

        if (wasRecording)
        {
            graphCtx.Close();
            Profiler.Stop();
            return;
        }

        graphCtx.PushClip(new RectangleGeometry(new Rect(0, headerHeight, graphWidth, graphHeight - headerHeight)));

        var threadProfilers = Profiler.GetProfilerGroups();
        Array.Sort(threadProfilers, ThreadGroupComparer);

        Profiler.Start("Draw group events");

        float y = headerHeight - (float)vScroll.Value;

        for (int i = 0; i < threadProfilers.Length; i++)
        {
            var colorHSV = GetColorHSV(i, threadProfilers.Length);
            int maxDepth = 0;

            DrawGroupEvents(graphCtx, threadProfilers[i], colorHSV, startTime, y, ref maxDepth);

            y += barHeight * maxDepth + threadGroupPadding;
        }

        Profiler.Stop();

        graphCtx.Close();

        Profiler.Stop();
    }

    void DrawHoverInfo(DrawingContext drawCtx, ProfilerGroup hoverGroup, double x, double y)
    {
        double graphHeight = ViewportHeight;

        Profiler.Start("Draw hover info");

        for (int i = 0; i < hoverEvents.Count; i++)
        {
            var (hoverSegment, hoverIndex) = hoverEvents[i];
            var segment = hoverGroup.Events[hoverSegment];
            ref var _event = ref segment[hoverIndex];

            float nameWidth = MeasureString(_event.Name, fontFace, FontSize).X;
            double barY = y + _event.Depth * barHeight;
            var tooltipArea = new Rect(x, barY, nameWidth, barHeight);

            var timeString = stringBuilder;

            if (!_event.IsSinglePoint)
            {
                timeString.AppendFormat("{0:n1}", _event.ElapsedTime.TotalMilliseconds * 1000).Append("µs");

                float timeStringWidth = MeasureString(timeString.ToString(), fontFace, FontSize).X;

                tooltipArea.Width = Math.Max(tooltipArea.Width, timeStringWidth);
                tooltipArea.Height += barHeight;
            }

            long memDelta = _event.MemoryAfter - _event.MemoryBefore;
            var memString = stringBuilder2;

            if (_event.MemoryTracked && memDelta > 0)
            {
                memString.AppendFormat("{0:n0}", memDelta).Append(" B allocated");

                float memStringWidth = MeasureString(memString.ToString(), fontFace, FontSize).X;

                tooltipArea.Width = Math.Max(tooltipArea.Width, memStringWidth);
                tooltipArea.Height += barHeight;
            }

            var extraString = stringBuilder3;

            if (_event.ExtraValueType != ProfilerEvent.ExtraValueTypeOption.None)
            {
                var formatString = _event.ExtraValueFormat ?? "Extra Value: {0:n0}";

                switch (_event.ExtraValueType)
                {
                case ProfilerEvent.ExtraValueTypeOption.Object:
                    extraString.AppendFormat(formatString, _event.ExtraObject);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.Long:
                    extraString.AppendFormat(formatString, _event.ExtraValue.LongValue);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.Double:
                    extraString.AppendFormat(formatString, _event.ExtraValue.DoubleValue);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.Float:
                    extraString.AppendFormat(formatString, _event.ExtraValue.FloatValue);
                    break;
                default:
                    break;
                }

                var extraStringSize = MeasureString(extraString.ToString(), fontFace, FontSize);

                tooltipArea.Width = Math.Max(tooltipArea.Width, extraStringSize.X);
                tooltipArea.Height += barHeight + extraStringSize.Y;
            }

            tooltipArea.Width += 12;
            tooltipArea.Height += 8;

            if (i == 0 && tooltipArea.Bottom > graphHeight)
                tooltipArea.Y -= tooltipArea.Bottom - graphHeight;

            drawCtx.DrawRectangle(hoverInfoBackgroundBrush, null, tooltipArea);

            tooltipArea.X += 6;
            tooltipArea.Y += 4;

            DrawText(drawCtx, _event.Name, fontFace, FontSize, Colors.White, tooltipArea.Location);

            if (!_event.IsSinglePoint)
            {
                tooltipArea.Y += barHeight;
                DrawText(drawCtx, timeString.ToString(), fontFace, FontSize, Colors.White, tooltipArea.Location);
                timeString.Clear();
            }

            if (memString.Length != 0)
            {
                tooltipArea.Y += barHeight;
                DrawText(drawCtx, memString.ToString(), fontFace, FontSize, Colors.White, tooltipArea.Location);
                memString.Clear();
            }

            if (extraString.Length != 0)
            {
                tooltipArea.Y += barHeight * 2;
                DrawText(drawCtx, extraString.ToString(), fontFace, FontSize, Colors.White, tooltipArea.Location);
                extraString.Clear();
            }

            y += tooltipArea.Height + 4;

            if (y > graphHeight)
                break;
        }

        hoverEvents.Clear();

        Profiler.Stop();
    }

    void DrawGroupEvents(DrawingContext drawCtx, ProfilerGroup group, Vector3 colorHSV, long startTicks, float y, ref int maxDepth)
    {
        if (group.NextEventIndex == 0)
            return;

        double graphWidth = ViewportWidth;

        int endSegmentIndex = (group.NextEventIndex - 1) / ProfilerGroup.EventBufferSegmentSize;

        for (int i = 0; i <= endSegmentIndex; i++)
        {
            var segment = group.Events[i];
            int endEventIndex = Math.Min(segment.Length, group.NextEventIndex - i * ProfilerGroup.EventBufferSegmentSize) - 1;

            if (segment[endEventIndex].EndTime - startTicks < -shiftX)
                continue;

            if (segment[0].StartTime - startTicks > -shiftX + (long)PixelsToTicks(graphWidth))
                break;

            for (int j = 0; j <= endEventIndex; j++)
            {
                ref var _event = ref segment[j];

                if (_event.Depth + 1 > maxDepth)
                    maxDepth = _event.Depth + 1;

                if (minifiedDrawStack.Length <= _event.Depth)
                {
                    int oldSize = minifiedDrawStack.Length;
                    Array.Resize(ref minifiedDrawStack, _event.Depth + 1);

                    for (int k = oldSize; k <= _event.Depth; k++)
                        minifiedDrawStack[k] = (-1, 0);
                }

                float startX = (float)TicksToPixels(_event.StartTime - startTicks + shiftX);
                float width = _event.IsSinglePoint ? 4 : (float)TicksToPixels(_event.EndTime - _event.StartTime);

                if (startX + width < 0 || startX > graphWidth)
                    continue;

                var hsv = colorHSV;
                // TODO: Fix double size dark band
                hsv.Z *= (float)Math.Pow(1 - 2 * Math.Abs(_event.Depth / 25f - Math.Floor(_event.Depth / 25f + 0.5f)), 2); // pow2 triangle wave, period 25
                hsv.Z = (float)Math.Sqrt(hsv.Z);

                var rgb = HSVtoRGB(hsv);
                SolidColorBrush? barBrush = null;

                if (!_event.IsSinglePoint)
                    barBrush = GetBrushForColor(rgb);

                ref var minif = ref minifiedDrawStack[_event.Depth];

                if (width < minBarWidth)
                {
                    float startXRound = (float)Math.Round(startX);
                    float fill = width / minBarWidth;

                    if (minif.StartX < 0)
                    {
                        minif.StartX = startXRound;
                    }
                    else
                    {
                        bool tooFar = startXRound - minif.StartX >= minBarWidth;
                        bool tooFull = minif.FillPercent + fill > 1;

                        if (tooFar || tooFull)
                        {
                            var miniArea = new Rect(minif.StartX, y + _event.Depth * barHeight, minBarWidth, Math.Max(minBarHeight, barHeight * minif.FillPercent));

                            if (tooFull && !tooFar)
                                miniArea.Height = barHeight;

                            drawCtx.DrawRectangle(barBrush, null, miniArea);

                            minif.StartX = startXRound;
                            minif.FillPercent = 0;
                        }
                    }

                    minif.FillPercent += fill;
                    continue;
                }

                if (minif.FillPercent > 0)
                {
                    var miniArea = new Rect(minif.StartX, y + _event.Depth * barHeight, minBarWidth, Math.Max(minBarHeight, barHeight * minif.FillPercent));

                    drawCtx.DrawRectangle(barBrush, null, miniArea);

                    minif.StartX = -1;
                    minif.FillPercent = 0;
                }

                var area = new Rect(startX, y + _event.Depth * barHeight, width, barHeight);

                if (_event.IsSinglePoint)
                {
                    var polyPoints = new Point[] {
                        //area.TopLeft,
                        area.TopRight,
                        new Point(area.Right - 1, area.Bottom),
                        new Point(area.Left + 1, area.Bottom)
                    };

                    drawCtx.DrawGeometry(eventBrush, null, new PathGeometry([new PathFigure(area.TopLeft, [new PolyLineSegment(polyPoints, isStroked: false)], closed: true)]));
                }
                else
                {
                    drawCtx.DrawRectangle(barBrush, null, area);
                }

                if (!_event.IsSinglePoint)
                {
                    var lumaConstants = new Vector3(0.2127f, 0.7152f, 0.0722f);
                    float barLuma = Vector3.Dot(rgb, lumaConstants);
                    float luma = 1 - barLuma;
                    var textColor = new Vector3(luma < 0.5f ? 0.1f : 1);

                    float w = width;

                    if (area.X < 0)
                    {
                        w += (float)area.X;
                        area.X = 0;
                    }

                    // One pixel of padding
                    area.X++;
                    w -= 2;

                    if (w > 0)
                    {
                        var textBrush = GetBrushForColor(textColor);

                        var ft = new FormattedText(_event.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                            fontFace, FontSize, textBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip) {
                            MaxTextWidth = w,
                            MaxLineCount = 1,
                            Trimming = TextTrimming.CharacterEllipsis
                        };

                        if (ft.Width <= w)
                            drawCtx.DrawText(ft, area.Location);
                    }
                }
            }
        }

        for (int depth = 0; depth < minifiedDrawStack.Length; depth++)
        {
            ref var minif = ref minifiedDrawStack[depth];

            if (minif.FillPercent > 0)
            {
                var miniArea = new Rect(minif.StartX, y + depth * barHeight, minBarWidth, Math.Max(minBarHeight, barHeight * minif.FillPercent));

                var hsv = colorHSV;
                // TODO: Fix double size dark band
                hsv.Z *= (float)Math.Pow(1 - 2 * Math.Abs(depth / 25f - Math.Floor(depth / 25f + 0.5f)), 2); // pow2 triangle wave, period 25
                hsv.Z = (float)Math.Sqrt(hsv.Z);

                var rgb = HSVtoRGB(hsv);
                var barBrush = GetBrushForColor(rgb);

                drawCtx.DrawRectangle(barBrush, null, miniArea);

                minif.StartX = -1;
                minif.FillPercent = 0;
            }

            minif.StartX = -1;
            minif.FillPercent = 0;
        }
    }

    SolidColorBrush GetBrushForColor(Vector3 color)
    {
        SolidColorBrush? barBrush;

        if (!solidBrushes.TryGetValue(color, out barBrush))
            solidBrushes[color] = barBrush = new SolidColorBrush(ToColor(color));

        return barBrush;
    }

    Vector2 MeasureString(string text, Typeface typeface, double emSize)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, emSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        return new Vector2((float)ft.Width, (float)ft.Height);
    }

    void DrawText(DrawingContext drawCtx, string text, Typeface typeface, double emSize, Color color, Point origin)
    {
        drawCtx.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, emSize, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip), origin);
    }

    static Vector3 GetColorHSV(int index, int count)
    {
        float hue = (float)index / count;

        const float saturation = 0.6f;

        return new Vector3(hue, saturation, 1f);
    }

    static Vector3 HSVtoRGB(Vector3 HSV)
    {
        var RGB = HUEtoRGB(HSV.X);
        return ((RGB - Vector3.One) * HSV.Y + Vector3.One) * HSV.Z;
    }

    static Vector3 HUEtoRGB(float H)
    {
        var rgb = Vector3.Abs(new Vector3(H * 6) - new Vector3(3, 2, 4));
        rgb.X -= 1;
        rgb.Y = 2 - rgb.Y;
        rgb.Z = 2 - rgb.Z;

        return Vector3.Clamp(rgb, Vector3.Zero, Vector3.One);
    }

    static Color NewColor(byte r, byte g, byte b, byte a)
    {
        return new Color { R = r, G = g, B = b, A = a };
    }

    static Color NewColor(byte r, byte g, byte b)
    {
        return new Color { R = r, G = g, B = b, A = 255 };
    }

    static Color ToColor(Vector3 vector)
    {
        return new Color {
            R = (byte)(vector.X * 255),
            G = (byte)(vector.Y * 255),
            B = (byte)(vector.Z * 255),
            A = 255
        };
    }
}
