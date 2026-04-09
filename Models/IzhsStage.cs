using System;
using System.Collections.Generic;

namespace SmartKostanay.Models
{
    public class IzhsStage
    {
        public int StageNumber { get; set; } // Номер этапа (1, 2 или 3) [cite: 56]
        public string Name { get; set; } // Название этапа [cite: 56]
        public string Status { get; set; } // PENDING | IN_PROGRESS | COMPLETED | OVERDUE [cite: 56, 62]
        public DateTime Deadline { get; set; } // Контрольная дата [cite: 56]
        public DateTime? CompletedAt { get; set; } // Дата фактического выполнения [cite: 56]
        public List<string> Documents { get; set; } = new List<string>(); // Ссылки на документы [cite: 56]
        public List<string> Photos { get; set; } = new List<string>(); // Ссылки на фото [cite: 56]
    }
}