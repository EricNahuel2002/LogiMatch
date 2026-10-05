using Application.RoutePlanning;

namespace UnitTests.Application;

public class RouteMatrixTests
{
    [Fact]
    public void Create_WithWrongValueCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => RouteMatrix.Create(2, [1, 2, 3]));
    }

    [Fact]
    public void Project_SingleNode_KeepsItsCell()
    {
        var matrix = Create(3, hasCompleteData: true);

        var projected = matrix.Project([1]);

        Assert.Equal(1, projected.Size);
        Assert.Equal(matrix[1, 1], projected[0, 0]);
    }

    [Fact]
    public void Project_ReindexesValuesInPlace()
    {
        var matrix = RouteMatrix.Create(
            4,
            [
                0, 1, 2, 3,
                10, 11, 12, 13,
                20, 21, 22, 23,
                30, 31, 32, 33
            ]);

        var projected = matrix.Project([1, 3]);

        Assert.Equal(2, projected.Size);
        Assert.Equal(11, projected[0, 0]);
        Assert.Equal(13, projected[0, 1]);
        Assert.Equal(31, projected[1, 0]);
        Assert.Equal(33, projected[1, 1]);
    }

    [Fact]
    public void Project_DropsNodesOutsideTheSelection()
    {
        var matrix = Create(3, hasCompleteData: true);

        var projected = matrix.Project([0, 2]);

        Assert.Equal(2, projected.Size);

        // The helper fills cells with their flat index, so node (from, to) is from * 3 + to.
        Assert.Equal(matrix[0, 0], projected[0, 0]);
        Assert.Equal(matrix[0, 2], projected[0, 1]);
        Assert.Equal(matrix[2, 0], projected[1, 0]);
        Assert.Equal(matrix[2, 2], projected[1, 1]);
    }

    [Fact]
    public void Project_KeepsUnreachableCellsUntouched()
    {
        var matrix = Create(
            3,
            hasCompleteData: false,
            overrides: [(1, 2, RouteMatrix.UnreachableValue)]);

        var projected = matrix.Project([1, 2]);

        Assert.Equal(RouteMatrix.UnreachableValue, projected[0, 1]);
        Assert.False(projected.HasCompleteData);
    }

    [Fact]
    public void Project_IncompleteSource_StaysIncomplete()
    {
        var matrix = Create(
            4,
            hasCompleteData: false,
            overrides: [(3, 0, RouteMatrix.UnreachableValue)]);

        var projected = matrix.Project([0, 1]);

        Assert.False(projected.HasCompleteData);
    }

    [Fact]
    public void Project_SingleNodeOfIncompleteSource_StaysIncomplete()
    {
        var matrix = Create(
            4,
            hasCompleteData: false,
            overrides: [(3, 0, RouteMatrix.UnreachableValue)]);

        var projected = matrix.Project([0]);

        Assert.False(projected.HasCompleteData);
    }

    [Fact]
    public void Project_NoNodes_Throws()
    {
        var matrix = Create(3, hasCompleteData: true);

        Assert.Throws<ArgumentException>(() => matrix.Project([]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Project_NodeOutsideTheMatrix_Throws(int node)
    {
        var matrix = Create(3, hasCompleteData: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => matrix.Project([node]));
    }

    [Fact]
    public void Project_RepeatedNode_Throws()
    {
        var matrix = Create(3, hasCompleteData: true);

        Assert.Throws<ArgumentException>(() => matrix.Project([1, 1]));
    }

    [Fact]
    public void Project_OutOfOrderNodes_Throws()
    {
        var matrix = Create(3, hasCompleteData: true);

        Assert.Throws<ArgumentException>(() => matrix.Project([2, 1]));
    }

    [Fact]
    public void Project_NullNodes_Throws()
    {
        var matrix = Create(3, hasCompleteData: true);

        Assert.Throws<ArgumentNullException>(() => matrix.Project(null!));
    }

    [Fact]
    public void Project_AllNodes_IsEquivalentToTheSource()
    {
        var matrix = Create(3, hasCompleteData: true);

        var projected = matrix.Project([0, 1, 2]);

        Assert.Equal(matrix.Size, projected.Size);

        for (var from = 0; from < matrix.Size; from++)
        {
            for (var to = 0; to < matrix.Size; to++)
            {
                Assert.Equal(matrix[from, to], projected[from, to]);
            }
        }
    }

    private static RouteMatrix Create(
        int size,
        bool hasCompleteData,
        IReadOnlyCollection<(int From, int To, long Value)>? overrides = null)
    {
        var values = new long[size * size];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = i;
        }

        foreach (var (from, to, value) in overrides ?? [])
        {
            values[(from * size) + to] = value;
        }

        return RouteMatrix.Create(size, values, hasCompleteData);
    }
}