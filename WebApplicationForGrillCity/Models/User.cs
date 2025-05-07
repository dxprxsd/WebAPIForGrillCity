using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class User
{
    public int Userid { get; set; }

    public string? Sname { get; set; }

    public string? Fname { get; set; }

    public string? Patronumic { get; set; }

    public string? Phonenumber { get; set; }

    public string? Userpassword { get; set; }

    public string? Userlogin { get; set; }

    public virtual ICollection<Myorder> Myorders { get; set; } = new List<Myorder>();
}
