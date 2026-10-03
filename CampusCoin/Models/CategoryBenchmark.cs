namespace CampusCoin.Models
{
    public class CategoryBenchmark
    {
        public int BenchmarkId { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public DateTime ForMonth { get; set; }
        public decimal AverageSpend { get; set; }
        public int StudentCount { get; set; }
        public DateTime ComputedAt { get; set; }
    }
}
