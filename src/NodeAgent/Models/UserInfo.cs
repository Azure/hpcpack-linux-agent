namespace NodeAgent.Models;

/*
 * NOTE
 * 
 * The original C++ code uses tuple. It's "typedef"ed as a class in C# here.
 * It can be refactored without the base tuple in the future.
 */
public class UserInfo : Tuple<string, bool, bool, bool, bool, string>
{
    public UserInfo(string item1, bool item2, bool item3, bool item4, bool item5, string item6)
        : base(item1, item2, item3, item4, item5, item6)
    {
    }
}
