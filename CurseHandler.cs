using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicCardMods
{
	internal class CurseHandler
	{

		public static bool IsACurse(CardInfo cardToTest)
		{
			for (int i = 0; i < cardToTest.categories.Length; i++)
			{
				if (cardToTest.categories[i] == WillsWackyManagers.Utils.CurseManager.instance.curseCategory)
					return true;
			}
			return false;
		}
	}
}
