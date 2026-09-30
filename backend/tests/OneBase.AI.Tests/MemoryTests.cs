using OneBase.AI.Memory;

namespace OneBase.AI.Tests;

public class MemoryTests
{
    [Fact]
    public void Long_topic_fits_the_column_together_with_the_ellipsis()
    {
        var topic = AiMemoryStore.Trim(new string('а', 1200), AiMemoryStore.TopicLength);

        Assert.Equal(AiMemoryStore.TopicLength, topic.Length);
        Assert.EndsWith("…", topic);
        Assert.Equal("короткая", AiMemoryStore.Trim("короткая", AiMemoryStore.TopicLength));
    }
}
