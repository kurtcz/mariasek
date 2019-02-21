using System;
namespace Mariasek.SharedClient
{
    public interface IStorageAccessor
    {
        bool CheckStorageAccess();
        void GetStorageAccess();
    }
}
