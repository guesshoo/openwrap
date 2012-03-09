namespace Tests.VisualStudio
{
    // Fixed for VS 2010 compiler error: Interop type XXX cannot be embedded. Use the applicable interface instead 
    //http://blogs.msdn.com/b/mshneer/archive/2009/12/07/interop-type-xxx-cannot-be-embedded-use-the-applicable-interface-instead.aspx
    internal abstract class EnvDteConstants
    {
        public const string vsWindowKindOutput = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}";
    }
}