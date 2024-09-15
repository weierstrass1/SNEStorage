namespace SNEStorage.Models
{
    public partial class ResourceTags
    {
        public long Id { get; set; }
        public long ResourceId { get; set; }
        public long TagId { get; set; }
        public virtual Resource? Resource { get; set; }
        public virtual Tag? Tag { get; set; }
    }
}
