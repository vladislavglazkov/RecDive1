using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metrics
{
    public static class Metrics
    {
        private static Dictionary<Guid,int> _count= new Dictionary<Guid, int>();
        public static int GetCount(Guid guid)
        {
            return _count[guid];
        }
        
        private static Dictionary<Guid,int> _locMax=new Dictionary<Guid, int>();
        public static int GetLocMax(Guid guid)
        {
            return _locMax[guid];
        }
        public static void Increment(Guid guid)
        {
            _count[guid]++;
            _locMax[guid] = Math.Max(_count[guid], _locMax[guid]);
        }
        public static void Decrement(Guid guid) { _count[guid]--; }
        public static void Init (Guid guid)
        {
            _count[guid]=0;
            _locMax[guid]=0;
        }
        public static void Clear(Guid guid)
        {
            _count.Remove(guid);
            _locMax.Remove(guid);


        }


    }
}
