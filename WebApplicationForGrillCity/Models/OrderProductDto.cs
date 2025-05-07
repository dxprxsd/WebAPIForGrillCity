namespace WebApplicationForGrillCity.Models
{
    public class OrderProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string? PhotoUrl { get; set; }
    }
}
