using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class ScalarTests
{
    [Fact]
    public void RootScalar_BuildScalarFieldRow_IsNumericInteger()
    {
        var state = EditorStateFactory.FromFiles("test_scalar.json");

        var row = EditorLogic.BuildScalarFieldRow(state);

        Assert.Equal("integer", row.FieldType);
        Assert.Equal(65m, row.NumericValue);
    }

    [Fact]
    public void BuildFieldRow_ForSelectedScalar_DelegatesToScalarFieldRow()
    {
        var state = EditorStateFactory.FromFiles("test_scalar.json");

        var viaDelegation = EditorLogic.BuildFieldRow(state, state.SelectedPath);
        var viaScalar = EditorLogic.BuildScalarFieldRow(state);

        Assert.Equal(viaScalar.FieldType, viaDelegation.FieldType);
        Assert.Equal(viaScalar.NumericValue, viaDelegation.NumericValue);
    }
}
