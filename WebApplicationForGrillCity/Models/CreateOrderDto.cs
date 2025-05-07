namespace WebApplicationForGrillCity.Models
{
    public class CreateOrderDto
    {
        public int ClientId { get; set; }
        public Dictionary<int, int> Products { get; set; } = new();
        public int? DiscountId { get; set; }
    }
}
