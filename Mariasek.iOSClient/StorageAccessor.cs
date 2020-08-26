using System;
using Mariasek.SharedClient;

namespace Mariasek.iOSClient
{
	public class StorageAccessor : IStorageAccessor
	{
        public bool CheckStorageAccess()
        {
            return true;
        }

        public void GetStorageAccess(bool force)
		{
			//no action needed on iOS
		}
	}
}
