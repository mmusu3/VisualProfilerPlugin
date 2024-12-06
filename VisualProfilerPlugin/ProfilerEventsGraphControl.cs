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

namespace VisualProfiler;

class ProfilerEventsGraphControl : Control
{
    static ProfilerEventsGraphControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ProfilerEventsGraphControl), new FrameworkPropertyMetadata(typeof(ProfilerEventsGraphControl)));
    }

    SolidColorBrush headerBrush;
    SolidColorBrush backgroundBrush;
    Pen intervalLinePen;
    SolidColorBrush pointEventBrush;
    Pen pointEventPen;
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

    ProfilerEventsRecording? recordedEvents;
    CombinedFrameEvents? combinedFramesEvents;
    Dictionary<int, int> groupMaxDepths = [];

    long startTime;
    long endTime;

    public bool CombineFrames
    {
        get => combineFrames;
        set
        {
            combineFrames = value;

            if (value && combinedFramesEvents == null && recordedEvents != null)
                combinedFramesEvents = ProfilerHelper.CombineFrames(recordedEvents);

            ResetZoom();
        }
    }
    bool combineFrames;

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
    (int EventIndex, int Count) hoverIndices = (-1, 0);
    Point mouseDownPos;
    bool zoomSelectionStarted;
    bool mousePanStarted;

    StringBuilder stringBuilder = new();
    StringBuilder stringBuilder2 = new();
    StringBuilder stringBuilder3 = new();

    Dictionary<Vector3, SolidColorBrush> solidBrushes = [];

    List<int> hoverEvents = [];
    (float StartX, float FillPercent)[] minifiedDrawStack = [];

    struct PointEvent
    {
        public Point Location;
    }

    List<PointEvent> pointEvents = [];

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
        pointEventBrush = new SolidColorBrush(Colors.Red);
        pointEventPen = new Pen(new SolidColorBrush(Colors.Black), 0.5);
        selectionBrush = new SolidColorBrush(NewColor(0, 0, 0, 100));
        selectionPen = new Pen(new SolidColorBrush(NewColor(150, 150, 150)), 2);
        hoverInfoBackgroundBrush = new SolidColorBrush(new Color { A = 190 });
        fontFace = FontFamily.GetTypefaces().First();
        FontSize = 14;

        hScroll = new ScrollBar { Orientation = Orientation.Horizontal, LargeChange = 500, SmallChange = 100 };
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

        if (zoom == minZoom)
            ResetZoom();
    }

    protected override void OnMouseMove(MouseEventArgs args)
    {
        mousePos = args.GetPosition(this);

        base.OnMouseMove(args);

        if (mousePanStarted)
        {
            var mouseDelta = mousePos - mouseDownPos;

            if (mouseDelta.X != 0)
            {
                long minShift = GetMinXShift();

                shiftX += (long)PixelsToTicks(mouseDelta.X);

                if (shiftX > minShift)
                    shiftX = minShift;

                hScroll.Value = TicksToPixels(-shiftX);
            }

            if (mouseDelta.Y != 0)
                vScroll.Value -= mouseDelta.Y;

            if (mouseDelta != default)
                InvalidateVisual(); // Redraw graph

            mouseDownPos = mousePos;
        }

        if (zoomSelectionStarted)
            RedrawSelection(mousePos);

        if (Profiler.IsRecordingEvents)
            return;

        (ProfilerEvent[] Events, ProfilerEventsSegment[] Segments)? hoverGroup = null;
        float hoverY = 0;
        float y = headerHeight - (float)vScroll.Value;
        bool reDraw = false;

        if (combineFrames && combinedFramesEvents != null)
        {
            foreach (var group in combinedFramesEvents.Groups)
            {
                ReadOnlySpan<ProfilerEventsSegment> segments = [new() {
                    StartTime = 0,
                    EndTime = group.Time,
                    StartIndex = 0,
                    Length = group.Events.Length
                }];

                GetHoveredEvents(group.Events, segments, 0, y);

                if (hoverEvents.Count > 0)
                {
                    int eventIndex = hoverEvents[0];

                    if (eventIndex != hoverIndices.EventIndex
                        || hoverEvents.Count != hoverIndices.Count)
                        reDraw = true;

                    hoverGroup = (group.Events, segments.ToArray());
                    hoverIndices = (eventIndex, hoverEvents.Count);
                    hoverY = y;
                    break;
                }

                if (groupMaxDepths.TryGetValue(group.ID, out int maxDepth))
                    y += barHeight * maxDepth + threadGroupPadding;
            }
        }
        else if (recordedEvents != null)
        {
            foreach (var (groupId, group) in recordedEvents.Groups)
            {
                GetHoveredEvents(group.Events, group.EventSegments, startTime, y);

                if (hoverEvents.Count > 0)
                {
                    int eventIndex = hoverEvents[0];

                    if (eventIndex != hoverIndices.EventIndex
                        || hoverEvents.Count != hoverIndices.Count)
                        reDraw = true;

                    hoverGroup = (group.Events, group.EventSegments);
                    hoverIndices = (eventIndex, hoverEvents.Count);
                    hoverY = y;
                    break;
                }

                if (groupMaxDepths.TryGetValue(groupId, out int maxDepth))
                    y += barHeight * maxDepth + threadGroupPadding;
            }
        }

        if (hoverGroup != null)
        {
            if (reDraw)
            {
                var ctx = hoverDrawing.RenderOpen();

                DrawHoverInfo(ctx, hoverGroup.Value.Events, x: 0, y: 0);
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
            hoverIndices = (-1, 0);
            hoverDrawing.RenderOpen().Close(); // Clear
        }
    }

    void GetHoveredEvents(ReadOnlySpan<ProfilerEvent> events, ReadOnlySpan<ProfilerEventsSegment> segments, long startTicks, float startY)
    {
        double graphWidth = ViewportWidth;

        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (segment.EndTime - startTicks < -shiftX)
                continue;

            if (segment.StartTime - startTicks > -shiftX + (long)PixelsToTicks(graphWidth))
                break;

            for (int j = 0; j < segment.Length; j++)
            {
                int eventIndex = segment.StartIndex + j;
                ref readonly var _event = ref events[eventIndex];
                double startX = TicksToPixels(_event.StartTime - startTicks + shiftX);
                double width = _event.IsSinglePoint ? 4 : TicksToPixels(_event.EndTime - _event.StartTime);

                if (startX + width < 0 || startX > graphWidth)
                    continue;

                float barY = startY + _event.Depth * barHeight;
                double floorX = Math.Floor(startX);

                if (mousePos.X >= floorX && mousePos.X < floorX + Math.Max(minBarWidth, width)
                    && mousePos.Y >= barY && mousePos.Y < barY + barHeight)
                {
                    hoverEvents.Add(eventIndex);
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

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs args)
    {
        base.OnMouseRightButtonDown(args);

        if (zoomSelectionStarted)
        {
            zoomSelectionStarted = false;
            selectionDrawing.RenderOpen().Close(); // Clear
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs args)
    {
        base.OnMouseDown(args);

        if (args.ChangedButton == MouseButton.Middle)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
            {
                mouseDownPos = args.GetPosition(this);
                mousePanStarted = true;
            }
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs args)
    {
        base.OnMouseUp(args);

        if (args.ChangedButton == MouseButton.Middle)
            mousePanStarted = false;
    }

    protected override void OnMouseLeave(MouseEventArgs args)
    {
        base.OnMouseLeave(args);

        if (mousePanStarted)
            mousePanStarted = false;

        if (zoomSelectionStarted)
        {
            zoomSelectionStarted = false;
            selectionDrawing.RenderOpen().Close(); // Clear
        }

        if (hoverIndices.EventIndex != -1)
        {
            hoverIndices = (-1, 0);
            hoverDrawing.RenderOpen().Close(); // Clear
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);

        long minShift = GetMinXShift();

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            double oldZoom = zoom;
            double delta = args.Delta * ((1.0 / 100) * 0.05) * zoom;

            Zoom = Math.Min(Math.Max(zoom + delta, minZoom * 0.9), maxZoom);

            var pos = args.GetPosition(this);

            double oldOffset = pos.X / (oldZoom / TimeSpan.TicksPerMillisecond);
            double newOffset = pos.X / (zoom / TimeSpan.TicksPerMillisecond);

            shiftX += (long)(newOffset - oldOffset);

            if (shiftX > minShift)
                shiftX = minShift;

            hScroll.Value = TicksToPixels(-shiftX);
        }
        else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            double scale = zoom / TimeSpan.TicksPerMillisecond;
            shiftX += (long)((args.Delta * ((1.0 / 120) * 55)) / scale);

            if (shiftX > minShift)
                shiftX = minShift;

            hScroll.Value = TicksToPixels(-shiftX);
        }
        else if (args.GetPosition(this).X < ViewportWidth)
        {
            vScroll.Value -= args.Delta;

            // TODO: Don't redraw, translate visual instead
        }

        InvalidateVisual(); // Redraw graph
    }

    long GetMinXShift()
    {
        long minShift;

        if (combineFrames && combinedFramesEvents != null)
            minShift = combinedFramesEvents.Time;
        else
            minShift = endTime - startTime;

        minShift /= 2;

        return minShift;
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
            minZoom = 1;
        else if (combineFrames && combinedFramesEvents != null)
            minZoom = ViewportWidth / (combinedFramesEvents.Time / (double)TimeSpan.TicksPerMillisecond);
        else
            minZoom = ViewportWidth / ((endTime - startTime) / (double)TimeSpan.TicksPerMillisecond);

        Zoom = minZoom;

        shiftX = 0;
        hScroll.Value = 0;
    }

    public void ZoomToFrame(int frameIndex)
    {
        if (recordedEvents == null)
        {
            ResetZoomInternal();
            return;
        }

        (long startTime, long endTime) = recordedEvents.GetTimeBoundsForFrame(frameIndex);

        if (startTime == long.MaxValue)
            Zoom = minZoom;
        else
            Zoom = ViewportWidth / ((endTime - startTime) / (double)TimeSpan.TicksPerMillisecond);

        shiftX = this.startTime - startTime;
        hScroll.Value = TicksToPixels(-shiftX);

        InvalidateVisual();
    }

    public void SetRecordedEvents(ProfilerEventsRecording? recording)
    {
        Profiler.Start(ProfilerKeys.SetRecordedEvents);

        startTime = long.MaxValue;
        endTime = 0;

        recordedEvents = recording;

        if (combineFrames && recording != null)
            combinedFramesEvents = ProfilerHelper.CombineFrames(recording);
        else
            combinedFramesEvents = null;

        groupMaxDepths.Clear();

        float y = 0;

        if (recording != null)
        {
            foreach (var (groupId, group) in recording.Groups)
            {
                GetEventTimeBounds(group.Events, ref startTime, ref endTime);

                int maxDepth = GetMaxDepthForGroup(group.Events);

                groupMaxDepths[groupId] = maxDepth;

                y += barHeight * maxDepth + threadGroupPadding;
            }
        }

        ContentHeight = (int)y;
        ResetZoomInternal();

        hoverIndices = (-1, 0);
        hoverDrawing.RenderOpen().Close(); // Clear

        InvalidateVisual();

        Profiler.Stop();
    }

    static void GetEventTimeBounds(ProfilerEvent[] events, ref long startTime, ref long endTime)
    {
        if (events.Length == 0)
            return;

        long start = events[0].StartTime;

        if (start < startTime)
            startTime = start;

        long end = events[^1].EndTime;

        if (end > endTime)
            endTime = end;
    }

    static int GetMaxDepthForGroup(ProfilerEvent[] events)
    {
        if (events.Length == 0)
            return 0;

        int maxDepth = 0;

        for (int i = 0; i < events.Length; i++)
        {
            ref readonly var _event = ref events[i];

            if (_event.Depth + 1 > maxDepth)
                maxDepth = _event.Depth + 1;
        }

        return maxDepth;
    }

    protected override void OnRender(DrawingContext drawCtx)
    {
        base.OnRender(drawCtx);

        double graphWidth = ViewportWidth;
        double graphHeight = ViewportHeight;

        if (graphWidth == 0 || graphHeight < headerHeight)
            return;

        var bgdCtx = backgroundDrawing.RenderOpen();

        bgdCtx.DrawRectangle(headerBrush, null, new Rect(0, 0, graphWidth, headerHeight));
        bgdCtx.DrawRectangle(backgroundBrush, null, new Rect(0, headerHeight, graphWidth, graphHeight - headerHeight));

        bgdCtx.Close();

        var graphCtx = graphDrawing.RenderOpen();

        if (recordedEvents == null)
        {
            graphCtx.Close();
            return;
        }

        Profiler.Start(ProfilerKeys.DrawProfilerEventsGraph);

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

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var sb = stringBuilder;

            for (int i = 0; i < lineCount; i++)
            {
                int x2 = (int)(x + i * lineInterval * zoom);
                graphCtx.DrawLine(intervalLinePen, new Point(x2, 16), new Point(x2, graphHeight));

                double time = (left - offset + (1 + i) * lineInterval) * unitScale;
                sb.AppendFormat("{0:N0}", time).Append(unit);

                var ft = new FormattedText(sb.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    fontFace, FontSize, Brushes.White, pixelsPerDip) { TextAlignment = TextAlignment.Center };

                graphCtx.DrawText(ft, new Point(x2, 0));

                sb.Clear();
            }
        }

        graphCtx.PushClip(new RectangleGeometry(new Rect(0, headerHeight, graphWidth, graphHeight - headerHeight)));

        if (combineFrames && combinedFramesEvents != null)
        {
            Profiler.Start(ProfilerKeys.DrawGroupEvents);

            float y = headerHeight - (float)vScroll.Value;
            int i = 0;

            foreach (var group in combinedFramesEvents.Groups)
            {
                var colorHSV = GetColorHSV(i++, combinedFramesEvents.Groups.Length);

                var segment = new ProfilerEventsSegment {
                    StartTime = 0,
                    EndTime = group.Time,
                    StartIndex = 0,
                    Length = group.Events.Length
                };

                DrawGroupEvents(graphCtx, group.Events, [segment], colorHSV, 0, y);

                y += barHeight * groupMaxDepths[group.ID] + threadGroupPadding;
            }

            Profiler.Stop();
        }
        else
        {
            Profiler.Start(ProfilerKeys.DrawGroupEvents);

            float y = headerHeight - (float)vScroll.Value;
            int i = 0;

            foreach (var (groupId, group) in recordedEvents.Groups)
            {
                var colorHSV = GetColorHSV(i++, recordedEvents.Groups.Count);

                DrawGroupEvents(graphCtx, group.Events, group.EventSegments, colorHSV, startTime, y);

                y += barHeight * groupMaxDepths[groupId] + threadGroupPadding;
            }

            Profiler.Stop();
        }

        graphCtx.Close();

        Profiler.Stop();
    }

    void DrawHoverInfo(DrawingContext drawCtx, ProfilerEvent[] events, double x, double y)
    {
        double graphHeight = ViewportHeight;

        Profiler.Start(ProfilerKeys.DrawHoverInfo);

        for (int i = 0; i < hoverEvents.Count; i++)
        {
            int hoverIndex = hoverEvents[i];
            ref readonly var _event = ref events[hoverIndex];

            float nameWidth = MeasureString(_event.Name, fontFace, FontSize).X;
            double barY = y + _event.Depth * barHeight;
            var tooltipArea = new Rect(x, barY, nameWidth, barHeight);

            var timeString = stringBuilder;

            if (!_event.IsSinglePoint)
            {
                timeString.AppendFormat("{0:n1}", _event.ElapsedMcroseconds).Append("µs");

                float timeStringWidth = MeasureString(timeString.ToString(), fontFace, FontSize).X;

                tooltipArea.Width = Math.Max(tooltipArea.Width, timeStringWidth);
                tooltipArea.Height += barHeight;
            }

            long memDelta = _event.MemoryAfter - _event.MemoryBefore;
            var memString = stringBuilder2;

            if (_event.MemoryTracked)
            {
                if (memDelta > 0)
                {
                    memString.AppendFormat("{0:n0}", memDelta).Append(" B allocated");

                    float memStringWidth = MeasureString(memString.ToString(), fontFace, FontSize).X;

                    tooltipArea.Width = Math.Max(tooltipArea.Width, memStringWidth);
                    tooltipArea.Height += barHeight;
                }
            }
            else if (!_event.IsSinglePoint)
            {
                memString.Append("N/A allocated");

                float memStringWidth = MeasureString(memString.ToString(), fontFace, FontSize).X;

                tooltipArea.Width = Math.Max(tooltipArea.Width, memStringWidth);
                tooltipArea.Height += barHeight;
            }

            var extraString = stringBuilder3;

            if (_event.ExtraValue.Type != ProfilerEvent.ExtraValueTypeOption.None)
            {
                var formatString = _event.ExtraValue.Format ?? "Extra Value: {0:n0}";

                switch (_event.ExtraValue.Type)
                {
                case ProfilerEvent.ExtraValueTypeOption.Object:
                    extraString.AppendFormat(formatString, _event.ExtraValue.Object);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory:
                    extraString.AppendFormat("Category: {0}", _event.ExtraValue.Value.CategoryValue);

                    if (_event.ExtraValue.Object != null)
                        extraString.AppendLine().AppendFormat(formatString, _event.ExtraValue.Object);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.Long:
                    extraString.AppendFormat(formatString, _event.ExtraValue.Value.LongValue);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.Double:
                    extraString.AppendFormat(formatString, _event.ExtraValue.Value.DoubleValue);
                    break;
                case ProfilerEvent.ExtraValueTypeOption.Float:
                    extraString.AppendFormat(formatString, _event.ExtraValue.Value.FloatValue);
                    break;
                default:
                    break;
                }

                tooltipArea.Height += barHeight; // Extra spacing

                var extraStringSize = MeasureString(extraString.ToString(), fontFace, FontSize);

                tooltipArea.Width = Math.Max(tooltipArea.Width, extraStringSize.X);
                tooltipArea.Height += extraStringSize.Y;
            }

            tooltipArea.Width += 12;
            tooltipArea.Height += 8;

            if (i == 0 && tooltipArea.Bottom > graphHeight)
                tooltipArea.Y -= tooltipArea.Bottom - graphHeight;

            drawCtx.DrawRectangle(hoverInfoBackgroundBrush, null, tooltipArea);

            tooltipArea.X += 6;
            tooltipArea.Y += 4;

            DrawText(drawCtx, _event.Name, fontFace, FontSize, Colors.White, tooltipArea.Location);
            tooltipArea.Y += barHeight;

            if (!_event.IsSinglePoint)
            {
                DrawText(drawCtx, timeString.ToString(), fontFace, FontSize, Colors.White, tooltipArea.Location);
                timeString.Clear();
                tooltipArea.Y += barHeight;
            }

            if (memString.Length != 0)
            {
                DrawText(drawCtx, memString.ToString(), fontFace, FontSize, Colors.White, tooltipArea.Location);
                memString.Clear();
                tooltipArea.Y += barHeight;
            }

            if (extraString.Length != 0)
            {
                tooltipArea.Y += barHeight; // Extra spacing
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

    void DrawGroupEvents(DrawingContext drawCtx, ProfilerEvent[] events, ProfilerEventsSegment[] segments, Vector3 colorHSV, long startTicks, float y)
    {
        double graphWidth = ViewportWidth;

        for (int s = 0; s < segments.Length; s++)
        {
            var segment = segments[s];

            if (segment.EndTime - startTicks < -shiftX)
                continue;

            if (segment.StartTime - startTicks > -shiftX + (long)PixelsToTicks(graphWidth))
                break;

            var segmentEvents = events.AsSpan(segment.StartIndex, segment.Length);

            for (int e = 0; e < segmentEvents.Length; e++)
            {
                ref readonly var _event = ref segmentEvents[e];

                if (minifiedDrawStack.Length <= _event.Depth)
                {
                    int oldSize = minifiedDrawStack.Length;

                    Array.Resize(ref minifiedDrawStack, _event.Depth + 1);

                    for (int k = oldSize; k <= _event.Depth; k++)
                        minifiedDrawStack[k] = (-1, 0);
                }

                double startX = TicksToPixels(_event.StartTime - startTicks + shiftX);
                double width = TicksToPixels(_event.EndTime - _event.StartTime);

                if (startX + width < 0 || startX > graphWidth)
                    continue;

                if (startX < -graphWidth)
                {
                    double w = -graphWidth;
                    width -= w - startX;
                    startX = w;
                }

                if (width > graphWidth * 2)
                    width = graphWidth * 2;

                var hsv = colorHSV;
                // TODO: Fix double size dark band
                hsv.Z *= (float)Math.Pow(1 - 2 * Math.Abs(_event.Depth / 25f - Math.Floor(_event.Depth / 25f + 0.5f)), 2); // pow2 triangle wave, period 25
                hsv.Z = (float)Math.Sqrt(hsv.Z);

                var rgb = HSVtoRGB(hsv);
                SolidColorBrush? barBrush = null;

                if (!_event.IsSinglePoint)
                    barBrush = GetBrushForColor(rgb);

                ref var minif = ref minifiedDrawStack[_event.Depth];

                if (!_event.IsSinglePoint && width < minBarWidth)
                {
                    float startXRound = (float)Math.Round(startX);
                    float fill = (float)(width / minBarWidth);

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
                    pointEvents.Add(new() { Location = area.Location });
                }
                else
                {
                    drawCtx.DrawRectangle(barBrush, null, area);

                    var lumaConstants = new Vector3(0.2127f, 0.7152f, 0.0722f);
                    float barLuma = Vector3.Dot(rgb, lumaConstants);
                    float luma = 1 - barLuma;
                    var textColor = new Vector3(luma < 0.5f ? 0.1f : 1);

                    double w = width;

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

        for (int i = 0; i < pointEvents.Count; i++)
        {
            var pos = pointEvents[i].Location;
            var topLeft = pos;
            topLeft.Offset(-3, 4);

            var topRight = pos;
            topRight.Offset(3, 4);

            var bottom = pos;
            bottom.Offset(0, barHeight - 4);

            var polyPoints = new Point[] { topRight, bottom };
            var geo = new PathGeometry([new PathFigure(topLeft, [new PolyLineSegment(polyPoints, isStroked: true)], closed: true)]);

            drawCtx.DrawGeometry(pointEventBrush, pointEventPen, geo);
        }

        pointEvents.Clear();
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

    static class ProfilerKeys
    {
        internal readonly static ProfilerKey SetRecordedEvents;
        internal readonly static ProfilerKey DrawProfilerEventsGraph;
        internal readonly static ProfilerKey DrawGroupEvents;
        internal readonly static ProfilerKey DrawHoverInfo;

        static ProfilerKeys()
        {
            SetRecordedEvents = ProfilerKeyCache.GetOrAdd("SetRecordedEvents");
            DrawProfilerEventsGraph = ProfilerKeyCache.GetOrAdd("Draw ProfilerEventsGraph");
            DrawGroupEvents = ProfilerKeyCache.GetOrAdd("Draw Group Events");
            DrawHoverInfo = ProfilerKeyCache.GetOrAdd("Draw Hover Info");
        }
    }
}
