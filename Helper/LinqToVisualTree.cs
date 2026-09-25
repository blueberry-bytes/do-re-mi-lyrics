using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Do_Re_Mi_Lyrics.Helper;

public class VisualTreeAdapter(DependencyObject item) : ILinqTree<DependencyObject>
{
    public IEnumerable<DependencyObject> Children()
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(item);
        for (int i = 0; i < childrenCount; i++)
        {
            yield return VisualTreeHelper.GetChild(item, i);
        }
    }
}

public interface ILinqTree<out T>
{
    IEnumerable<T> Children();
}

public static class LinqToVisualTree
{
    public static IEnumerable<DependencyObject> Descendants(this DependencyObject item)
    {
        ILinqTree<DependencyObject> adapter = new VisualTreeAdapter(item);
        foreach (DependencyObject child in adapter.Children())
        {
            yield return child;

            foreach (DependencyObject grandChild in child.Descendants())
            {
                yield return grandChild;
            }
        }
    }
}