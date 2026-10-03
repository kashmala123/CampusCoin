namespace CampusCoin.Models
{
    public class CsvImportBatch
    {
        public int ImportId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int RowsTotal { get; set; }
        public int RowsImported { get; set; }
        public int RowsFailed { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed
        public DateTime ImportedAt { get; set; }
    }
}
