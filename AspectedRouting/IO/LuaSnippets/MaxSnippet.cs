using AspectedRouting.Language;

namespace AspectedRouting.IO.LuaSnippets
{
    public class MaxSnippet : ListFoldingSnippet
    {
        public MaxSnippet() : base(Funcs.Max, "nil") { }
        public override string Combine(string assignTo, string value)
        {
            return Utils.Lines("if ( " + assignTo + " == nil or " + assignTo + " < " + value + " ) then",
                "    " + assignTo + " = " + value,
                "end");
        }
    }
}