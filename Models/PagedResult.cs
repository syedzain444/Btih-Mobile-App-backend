namespace HospitalMobileAPPApi.Models
{
    public class PagedResult<T>
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public IReadOnlyList<T> Data { get; set; } = Array.Empty<T>();
    }
}
