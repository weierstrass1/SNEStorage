using Microsoft.AspNetCore.Identity;

namespace SNEStorage.Models;
public partial class ScoreUsers
{
    public long Id { get; set; }
    public long ScoreId { get; set; }
    public long UserId { get; set; }
    public long Value {  get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual Score? Score { get; set; }
}
