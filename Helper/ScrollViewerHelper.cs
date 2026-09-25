using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Do_Re_Mi_Lyrics.Helper;

public static class ScrollViewerHelper
{
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(ScrollViewerHelper),
            new PropertyMetadata(0.0, OnVerticalOffsetPropertyChanged));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.RegisterAttached("HorizontalOffset", typeof(double), typeof(ScrollViewerHelper),
            new PropertyMetadata(0.0, OnHorizontalOffsetPropertyChanged));


    private static readonly DependencyProperty HorizontalScrollBarProperty =
        DependencyProperty.RegisterAttached("HorizontalScrollBar", typeof(ScrollBar), typeof(ScrollViewerHelper),
            new PropertyMetadata(null));

    private static readonly DependencyProperty VerticalScrollBarProperty =
        DependencyProperty.RegisterAttached("VerticalScrollBar", typeof(ScrollBar), typeof(ScrollViewerHelper),
            new PropertyMetadata(null));

    public static double GetVerticalOffset(DependencyObject depObj)
    {
        return (double) depObj.GetValue(VerticalOffsetProperty);
    }

    public static void SetVerticalOffset(DependencyObject depObj, double value)
    {
        depObj.SetValue(VerticalOffsetProperty, value);
    }

    public static double GetHorizontalOffset(DependencyObject depObj)
    {
        return (double) depObj.GetValue(HorizontalOffsetProperty);
    }

    public static void SetHorizontalOffset(DependencyObject depObj, double value)
    {
        depObj.SetValue(HorizontalOffsetProperty, value);
    }

    private static void OnVerticalOffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv)
        {
            return;
        }

        if (sv.GetValue(VerticalScrollBarProperty) == null)
        {
            sv.LayoutUpdated += (_, _) =>
            {
                if (sv.GetValue(VerticalScrollBarProperty) == null)
                {
                    GetScrollBarsForScrollViewer(sv);
                }
            };
        }
        else
        {
            sv.ScrollToVerticalOffset((double) e.NewValue);
        }
    }

    private static void OnHorizontalOffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv)
        {
            return;
        }

        if (sv.GetValue(HorizontalScrollBarProperty) == null)
        {
            sv.LayoutUpdated += (_, _) =>
            {
                if (sv.GetValue(HorizontalScrollBarProperty) == null)
                {
                    GetScrollBarsForScrollViewer(sv);
                }
            };
        }
        else
        {
            sv.ScrollToHorizontalOffset((double) e.NewValue);
        }
    }

    private static void GetScrollBarsForScrollViewer(ScrollViewer scrollViewer)
    {
        ScrollBar? scroll = GetScrollBar(scrollViewer, Orientation.Vertical);
        scrollViewer.SetValue(VerticalScrollBarProperty, scroll);

        scrollViewer.ScrollToVerticalOffset(GetVerticalOffset(scrollViewer));

        if (scroll != null)
        {
            scroll.ValueChanged += (_, e) => { SetVerticalOffset(scrollViewer, e.NewValue); };
        }

        scroll = GetScrollBar(scrollViewer, Orientation.Horizontal);
        scrollViewer.SetValue(HorizontalScrollBarProperty, scroll);

        scrollViewer.ScrollToHorizontalOffset(GetHorizontalOffset(scrollViewer));

        if (scroll != null)
        {
            scroll.ValueChanged += (_, e) => { scrollViewer.SetValue(HorizontalOffsetProperty, e.NewValue); };
        }
    }

    private static ScrollBar? GetScrollBar(DependencyObject fe, Orientation orientation)
    {
        return fe.Descendants().OfType<ScrollBar>().SingleOrDefault(s => s.Orientation == orientation);
    }
}