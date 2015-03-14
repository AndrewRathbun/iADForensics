using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using Microsoft.Isam.Esent.Interop;

namespace iESELibrary.ESEHandler
{
    public class ESEDatabase : IDisposable
	{
		#region Variables
		private bool Disposed;

		private string eseDbFilePath;
		private JET_INSTANCE jetInstance;
		private JET_SESID jetSesId;
		private JET_DBID jetDbId;
		private JET_TABLEID jetTableId;
		private string tableName;

		private int jetValueHandle = 0;
		private string jetBuffer;

		private SortedList<string, SortedList<string, ColumnInfo>> databaseTables;

		#endregion

		#region Enums
		public enum JetColumn
		{
			objectSid =					0x00001,
			sAMAccountName =			0x00002,
			sAMAccountType =			0x00004,
			userPrincipalName =			0x00008,
			userAccountControl =		0x00010,
			lastLogon =					0x00020,
			lastLogonTimestamp =		0x00040,
			accountExpires =			0x00080,
			pwdLastSet =				0x00100,
			badPasswordTime =			0x00200,
			logonCount =				0x00400,
			badPwdCount =				0x00800,
			primaryGroupID =			0x01000,
			EncryptedNTHash =			0x02000,
			EncryptedLMHash =			0x04000,
			EncryptedNTHashHistory =	0x08000,
			EncryptedLMHashHistory =	0x10000,
			unixPassword =				0x20000,
			ADUserObjects =				0x40000,
			supplementCredentials =		0x80000
		}
		#endregion

		#region Constructors
		public ESEDatabase(string eseNtdsFilePath)
		{
			this.OpenDatabase(eseNtdsFilePath);

			//load tables definition
			this.databaseTables = new SortedList<string, SortedList<string, ColumnInfo>>(StringComparer.OrdinalIgnoreCase);

			foreach(string tableName in Api.GetTableNames(this.jetSesId, this.jetDbId) )
			{
				this.databaseTables.Add(tableName, new SortedList<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase));
				foreach( ColumnInfo columnInfo in Api.GetTableColumns(this.jetSesId, this.jetDbId, tableName) )
				{
					this.databaseTables[tableName].Add(columnInfo.Name, columnInfo);
				}
			}
		}

		#endregion

		#region Public Methods
		public Queue<SortedList<string, string>> RetrieveTranslatedAttributes(string eseDbTableName, JetColumn attributes)
		{
			Queue<SortedList<string, string>> result = new Queue<SortedList<string,string>>();

			try
			{ 
				if(Api.TryOpenTable(this.jetSesId, this.jetDbId, eseDbTableName, OpenTableGrbit.ReadOnly, out this.jetTableId))
				{
					this.tableName = eseDbTableName;

					int recordsTotal = -1;
					Api.JetIndexRecordCount(this.jetSesId, this.jetTableId, out recordsTotal, 0);

					while(recordsTotal > 1)
					{
						Api.JetMove(this.jetSesId, this.jetTableId, JET_Move.Next, MoveGrbit.None);

						SortedList<string, string> row = new SortedList<string,string>(StringComparer.OrdinalIgnoreCase);
						result.Enqueue(this.ReadRowAttributes(attributes));

						recordsTotal--;
					}
				}
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}

			return result;
		}
		#endregion

		#region Private Methods
		private void OpenDatabase(string eseNtdsFilePath)
		{
			try
			{
				this.eseDbFilePath = eseNtdsFilePath;

				string dbFileDirectory = System.IO.Path.GetDirectoryName(eseNtdsFilePath) + "\\";

				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.DatabasePageSize, 8192, null);
				Api.JetCreateInstance(out jetInstance, "msADDS");


				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.LogFilePath, null, dbFileDirectory);
				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.SystemPath, null, dbFileDirectory);
				//Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.TempPath, null, @"C:\Temp");
				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.BaseName, null, "edb");

				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.CreatePathIfNotExist, 1, null);
				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.DbExtensionSize, 256, null);
				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.CacheSize, 10, null);
				Api.JetSetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.CircularLog, 1, null);

				Api.JetGetSystemParameter(jetInstance, JET_SESID.Nil, JET_param.DatabasePageSize, ref jetValueHandle, out jetBuffer, 1024);

				Api.JetInit(ref jetInstance);

				Api.JetBeginSession(jetInstance, out jetSesId, null, null);

				Api.JetAttachDatabase(jetSesId, eseNtdsFilePath, AttachDatabaseGrbit.ReadOnly);
				Api.JetOpenDatabase(jetSesId, eseNtdsFilePath, null, out jetDbId, OpenDatabaseGrbit.ReadOnly);
			}
			catch (Exception eX)
			{
				throw new Exception(string.Format("{0}::{1}", new StackFrame(0, true).GetMethod().Name, eX.Message));
			}
		}

		/// <summary>
		/// Method translates attributes to string,string collection
		/// </summary>
		/// <param name="attributes">JetColumn attributes to translate</param>
		/// <returns></returns>
		private SortedList<string, string> ReadRowAttributes(JetColumn attributes)
		{
			SortedList<string, string> result = new SortedList<string, string>(StringComparer.OrdinalIgnoreCase);

			if( (attributes & JetColumn.objectSid)>0 )
			{
				string internalName = this.GetAttributeInternalName(JetColumn.objectSid);
				byte[] value = Api.RetrieveColumn(this.jetSesId, this.jetTableId, this.databaseTables[this.tableName][internalName].Columnid);
				if (value != null)
				{
					result.Add(JetColumn.objectSid.ToString(), new SecurityIdentifier(value, 0).Value);
				}
				else
				{
					result.Add(JetColumn.objectSid.ToString(), null);
				}
			}
			
			if( (attributes & JetColumn.sAMAccountName)>0)
			{
				string internalName = this.GetAttributeInternalName(JetColumn.sAMAccountName);
				byte[] value = Api.RetrieveColumn(this.jetSesId, this.jetTableId, this.databaseTables[this.tableName][internalName].Columnid);
				if (value != null)
				{
					result.Add(JetColumn.sAMAccountName.ToString(), Encoding.Unicode.GetString(value));
				}
				else
				{
					result.Add(JetColumn.sAMAccountName.ToString(), null);
				}
			}

			if ((attributes & JetColumn.userAccountControl) > 0)
			{
				string internalName = this.GetAttributeInternalName(JetColumn.userAccountControl);
				byte[] value = Api.RetrieveColumn(this.jetSesId, this.jetTableId, this.databaseTables[this.tableName][internalName].Columnid);
					
				if (value != null)
				{
					result.Add(JetColumn.userAccountControl.ToString(), BitConverter.ToUInt32(value, 0).ToString());
				}
				else
				{
					result.Add(JetColumn.userAccountControl.ToString(), null);
				}
			}

			if ((attributes & JetColumn.EncryptedNTHash) > 0)
			{
				string internalName = this.GetAttributeInternalName(JetColumn.EncryptedNTHash);
				byte[] value = Api.RetrieveColumn(this.jetSesId, this.jetTableId, this.databaseTables[this.tableName][internalName].Columnid);

				if (value != null)
				{
					result.Add(JetColumn.EncryptedNTHash.ToString(), BitConverter.ToUInt32(value, 0).ToString());
				}
				else
				{
					result.Add(JetColumn.EncryptedNTHash.ToString(), null);
				}
			}

			return result;
		}

		/// <summary>
		/// Retrieves transated attribute value as a string
		/// Assumes attributes are single valued
		/// </summary>
		/// <param name="attributeValue">Attribute value as byte array</param>
		/// <param name="attributeName">Attribute name as JetColumn</param>
		/// <returns></returns>
		private string TranslateAttributeAsString(byte[] attributeValue, JetColumn attributeName)
		{
			switch (attributeName)
			{
				case JetColumn.objectSid:
					{
						return new SecurityIdentifier(attributeValue, 0).Value;
					};
				case JetColumn.lastLogon:
					{
						Int64 lVal = BitConverter.ToInt64(attributeValue, 0);

						if (lVal > 0)
						{
							return String.Format("{0:yyyy-MM-dd hh:mm:ss}", DateTime.FromFileTime(lVal));
						}
						return null;
					};
			}
			return Encoding.Unicode.GetString(attributeValue);
		}
		private string[] GetAttributeInternalNames(JetColumn jetColumns)
		{
			List<string> result = new List<string>();
			if ( (jetColumns & JetColumn.objectSid) > 0 )
				result.Add("ATTr589970");
			if ((jetColumns & JetColumn.sAMAccountName) > 0)
				result.Add("ATTm590045");
			if ((jetColumns & JetColumn.sAMAccountType) > 0)
				result.Add("ATTj590126");
			if ((jetColumns & JetColumn.userPrincipalName ) > 0)
				result.Add("ATTm590480");
			if ((jetColumns & JetColumn.userAccountControl) > 0)
				result.Add("ATTj589832");
			if ((jetColumns & JetColumn.lastLogon) > 0)
				result.Add("ATTq589876");
			if ((jetColumns & JetColumn.lastLogonTimestamp) > 0)
				result.Add("ATTq591520");
			if ((jetColumns & JetColumn.accountExpires) > 0)
				result.Add("ATTq589983");
			if ((jetColumns & JetColumn.pwdLastSet) > 0)
				result.Add("ATTq589920");
			if ((jetColumns & JetColumn.badPasswordTime) > 0)
				result.Add("ATTq589873");
			if ((jetColumns & JetColumn.logonCount) > 0)
				result.Add("ATTj589993");
			if ((jetColumns & JetColumn.badPwdCount) > 0)
				result.Add("ATTj589836");
			if ((jetColumns & JetColumn.primaryGroupID) > 0)
				result.Add("ATTj589922");
			if ((jetColumns & JetColumn.EncryptedNTHash) > 0)
				result.Add("ATTk589914");
			if ((jetColumns & JetColumn.EncryptedLMHash) > 0)
				result.Add("ATTk589879");
			if ((jetColumns & JetColumn.EncryptedNTHashHistory) > 0)
				result.Add("ATTk589918");
			if ((jetColumns & JetColumn.EncryptedLMHashHistory) > 0)
				result.Add("ATTk589984");
			if ((jetColumns & JetColumn.unixPassword) > 0)
				result.Add("ATTk591734");
			if ((jetColumns & JetColumn.ADUserObjects) > 0)
				result.Add("ATTk36");
			if ((jetColumns & JetColumn.supplementCredentials) > 0)
				result.Add("ATTk589949");

			return result.ToArray();
		}

		private string GetAttributeInternalName(JetColumn jetColumn)
		{
			return GetAttributeInternalNames(jetColumn)[0];
		}
		#endregion

		#region IDisposable Members
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool bDisposing)
		{
			if (!this.Disposed)
			{
				if (bDisposing)
				{
					if(this.jetInstance != null)
					{
						Api.JetCloseDatabase(this.jetSesId, this.jetDbId, CloseDatabaseGrbit.None);
						Api.JetDetachDatabase(this.jetSesId, eseDbFilePath);
						Api.JetTerm(this.jetInstance);
					}
				}

				this.Disposed = true;
			}
		}
		#endregion
	}
}
