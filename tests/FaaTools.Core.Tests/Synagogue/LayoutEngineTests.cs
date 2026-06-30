using FaaTools.Core.Synagogue.Layout;
using FaaTools.Core.Synagogue.Model;

namespace FaaTools.Core.Tests.Synagogue;

public class LayoutEngineTests
{
    private static SpaceSnapshotNode Leaf(string name, double area, double occupants)
        => new(3, "a.", name, area, occupants, []);

    private static SpaceSnapshotNode Branch(int level, string marker, string name, double area, double occupants, params SpaceSnapshotNode[] children)
        => new(level, marker, name, area, occupants, children);

    [StaFact]
    public void LayoutForest_returns_empty_result_for_no_roots()
    {
        var result = LayoutEngine.LayoutForest([], 1000, 800);

        Assert.Empty(result.Roots);
        Assert.Equal(1.0, result.ScaleFactor);
    }

    [StaFact]
    public void LayoutForest_keeps_all_boxes_within_available_bounds()
    {
        var roots = new List<SpaceSnapshotNode>
        {
            Branch(1, "A.", "Worship Program", 4000, 250,
                Branch(2, "1.", "Sanctuary", 4000, 250, Leaf("Main Hall", 4000, 250))),
            Branch(1, "B.", "Social Hall", 1500, 50),
            Branch(1, "C.", "Education", 3000, 0,
                Branch(2, "1.", "Classroom A", 1000, 0),
                Branch(2, "2.", "Classroom B", 2000, 0)),
        };

        const double availableWidth = 900.0;
        const double availableHeight = 600.0;
        var result = LayoutEngine.LayoutForest(roots, availableWidth, availableHeight);

        Assert.Equal(3, result.Roots.Count);
        Assert.True(result.ScaleFactor > 0);

        foreach (var node in AllNodes(result.Roots))
        {
            Assert.True(node.BoxX >= -0.5, $"{node.Name} BoxX={node.BoxX} should be within bounds");
            Assert.True(node.BoxY >= -0.5, $"{node.Name} BoxY={node.BoxY} should be within bounds");
            Assert.True(node.BoxX + node.BoxSize <= availableWidth + 0.5, $"{node.Name} right edge exceeds available width");
        }
    }

    [StaFact]
    public void LayoutForest_does_not_overlap_sibling_root_boxes()
    {
        var roots = new List<SpaceSnapshotNode>
        {
            Branch(1, "A.", "Root One", 4000, 250),
            Branch(1, "B.", "Root Two", 1500, 50),
        };

        var result = LayoutEngine.LayoutForest(roots, 1200, 600);
        var a = result.Roots[0];
        var b = result.Roots[1];

        var aRight = a.BoxX + a.BoxSize;
        var bLeft = b.BoxX;
        Assert.True(aRight <= bLeft + 0.01, $"Root boxes overlap: aRight={aRight}, bLeft={bLeft}");
    }

    [StaFact]
    public void LayoutForest_scales_down_when_content_exceeds_available_space()
    {
        var roots = Enumerable.Range(0, 8)
            .Select(i => Branch(1, "A.", $"Root {i}", 4000, 100))
            .ToList();

        var result = LayoutEngine.LayoutForest(roots, 400, 300);

        Assert.True(result.ScaleFactor < 1.0, "Expected the forest to scale down to fit a too-small area");
        foreach (var node in AllNodes(result.Roots))
        {
            Assert.True(node.BoxX + node.BoxSize <= 400 + 0.5);
        }
    }

    private static IEnumerable<LayoutNode> AllNodes(IEnumerable<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in AllNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
