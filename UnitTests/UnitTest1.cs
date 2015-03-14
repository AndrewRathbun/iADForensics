using System;
using System.Collections.Generic;
using iESELibrary.ESEHandler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
	[TestClass]
	public class UnitTest1
	{
		[TestMethod]
		public void TestMethod1()
		{
			using(ESEDatabase eseDb = new ESEDatabase(@"C:\Projects\Evaluation013\tstglobal\ntds.dit"))
			{
				Queue<SortedList<string, string>> result = eseDb.RetrieveTranslatedAttributes("datatable",
					ESEDatabase.JetColumn.objectSid
					| ESEDatabase.JetColumn.sAMAccountName
					| ESEDatabase.JetColumn.userAccountControl
					| ESEDatabase.JetColumn.EncryptedNTHash
					);
			}
		}
	}
}
