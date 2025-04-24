using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Invoice_Management_System
{
    public class Product : INotifyPropertyChanged
    {
        [Key]
        public int Id { get; set; }

        private string _supplier = string.Empty;
        public string Supplier
        {
            get => _supplier;
            set
            {
                _supplier = value;
                OnPropertyChanged(nameof(Supplier));
            }
        }

        private string _productCode = string.Empty;
        public string ProductCode
        {
            get => _productCode;
            set
            {
                _productCode = value;
                OnPropertyChanged(nameof(ProductCode));
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        private decimal _stock;
        public decimal Stock
        {
            get => _stock;
            set
            {
                _stock = value;
                OnPropertyChanged(nameof(Stock));
                OnPropertyChanged(nameof(TotalValue)); // ανανέωση
            }
        }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
                OnPropertyChanged(nameof(TotalValue)); // ανανέωση
            }
        }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public decimal TotalValue => Stock * Price;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
