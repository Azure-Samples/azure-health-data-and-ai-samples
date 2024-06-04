using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents
{
    public static class AppUtility
    {
        public static string GetEnvironmentVariable(string varname, string defval = null)
        {
            if (string.IsNullOrEmpty(varname)) return null;
            string retVal = System.Environment.GetEnvironmentVariable(varname);
            if (defval != null && retVal == null) return defval;
            return retVal;
        }
        public static bool GetBoolEnvironmentVariable(string varname, bool defval = false)
        {
            var s = GetEnvironmentVariable(varname);
            if (string.IsNullOrEmpty(s)) return defval;
            if (s.Equals("1") || s.Equals("yes", System.StringComparison.InvariantCultureIgnoreCase) || s.Equals("true", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            if (s.Equals("0") || s.Equals("no", System.StringComparison.InvariantCultureIgnoreCase) || s.Equals("false", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }
            throw new Exception($"GetBoolEnvironmentVariable: Unparsable boolean environment variable for {varname} : {s}");
        }
        public static int GetIntEnvironmentVariable(string varname, string defval = null)
        {


            string retVal = System.Environment.GetEnvironmentVariable(varname);
            if (defval != null && retVal == null) retVal = defval;
            return int.Parse(retVal);
        }

        //public static IEMPIProvider EMPIProviderGetInstance(string assemblyclassfullname)
        //{
        //    Type t = Type.GetType(assemblyclassfullname);
        //    return Activator.CreateInstance(t) as IEMPIProvider;
        //}
    }
}
