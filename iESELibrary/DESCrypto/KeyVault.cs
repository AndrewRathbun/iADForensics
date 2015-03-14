using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iESELibrary.DESCrypto
{
	class KeyVault
	{
		#region Variables
		private byte[] key;
		#endregion

		#region Properties
		public byte[] Key
		{
			get
			{
				return this.key;
			}
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Initiates KeyVault for a given SYSTEM registry file
		/// </summary>
		/// <param name="filePath">SYSTEM registry file full path</param>
		public KeyVault(string filePath)
		{
				
		}
		#endregion

		#region Private Methods

		#endregion
	}
}
