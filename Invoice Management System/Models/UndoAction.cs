using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Invoice_Management_System.Models
{
    public enum UndoType
    {
        Add,
        Delete,
        Update
    }

    public class UndoAction
    {
        public UndoType ActionType { get; set; }
        public Product ProductBefore { get; set; }
        public Product? ProductAfter { get; set; }
    }
}
