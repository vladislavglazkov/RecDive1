using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Metrics
{
    public static class Metrics
    {
        private static Dictionary<Guid,int> _count= new Dictionary<Guid, int>();
        private static Dictionary<Guid, int> _limit = new Dictionary<Guid, int>();
        private static Dictionary<Guid, bool> _exceeded= new Dictionary<Guid, bool>();
        public static bool CheckExceeded(Guid id)
        {
            return _exceeded[id];
        }
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
            if (_count[guid] > _limit[guid])
            {
                _exceeded[guid] = true;
            }
            _locMax[guid] = Math.Max(_count[guid], _locMax[guid]);
        }
        public static void Decrement(Guid guid) { _count[guid]--; }
        public static void Init (Guid guid,int limit)
        {
            _count[guid]=0;
            _locMax[guid]=0;
            _limit[guid]=limit;
            _exceeded[guid] = false;
        }
        public static void Clear(Guid guid)
        {
            _count.Remove(guid);
            _locMax.Remove(guid);
            _limit.Remove(guid);
            _exceeded.Remove(guid);

        }


    }
}
