using Xunit;

// Усі тести працюють з однією тестовою базою, тому виконуються послідовно
[assembly: CollectionBehavior(DisableTestParallelization = true)]
