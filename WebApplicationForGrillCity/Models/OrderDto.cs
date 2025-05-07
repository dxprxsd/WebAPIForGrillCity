namespace WebApplicationForGrillCity.Models
{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public DateTime DateOfOrder { get; set; }
        public string CodeForTakeProduct { get; set; }
        public string StatusName { get; set; }
        public List<OrderProductDto> Products { get; set; } = new();
    }
}
