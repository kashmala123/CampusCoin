namespace CampusCoin.Models
{
    public class SimulationLog
    {
        public int SimulationId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string SimulationType { get; set; } = string.Empty; // CanIAffordIt, WhatIfSave, WhatIfSimulator
        public string? InputSummary { get; set; }
        public string? ResultSummary { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
