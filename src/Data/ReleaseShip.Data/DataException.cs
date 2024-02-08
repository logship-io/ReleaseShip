using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseShip.Data
{
    public class DataException : Exception
    {
        public DataException()
        {
        }

        public DataException(string? message) : base(message)
        {
        }

        public DataException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
