using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    public class GenericEqualityComparer<T> : IEqualityComparer<T>
    {
        Func<T, T, bool> compareFunction;
        Func<T, int> hashFunction;

        public GenericEqualityComparer(Func<T, T, bool> compareFunction, Func<T, int> hashFunction)
        {
            this.compareFunction = compareFunction;
            this.hashFunction = hashFunction;
        }

        public static GenericEqualityComparer<T> CreateComparer(Func<T, T, bool> compareFunction, Func<T, int> hashFunction)
        {
            return new GenericEqualityComparer<T>(compareFunction, hashFunction);
        }

        public bool Equals(T x, T y)
        {
            return compareFunction(x, y);
        }

        public int GetHashCode(T obj)
        {
            return hashFunction(obj);
        }
    }
}
