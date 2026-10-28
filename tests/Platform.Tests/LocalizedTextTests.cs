using DynamicCube.Platform.Localization;
using Shouldly;

namespace DynamicCube.Platform.Tests;

public sealed class LocalizedTextTests
{
    [Fact]
    public void Returns_requested_language() =>
        new LocalizedText("التشييد والبناء", "Construction").Get(Language.En).ShouldBe("Construction");

    [Fact]
    public void Falls_back_to_other_language_when_empty() =>
        new LocalizedText("التشييد والبناء", "").Get(Language.En).ShouldBe("التشييد والبناء");
}
