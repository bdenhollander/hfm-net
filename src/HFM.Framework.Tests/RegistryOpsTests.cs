/*
 * HFM.NET - Registry Operations Helper Class Tests
 * Copyright (C) 2009-2010 Ryan Harlamert (harlam357)
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; version 2
 * of the License. See the included file GPLv2.TXT.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;

using NUnit.Framework;

namespace HFM.Framework.Tests
{
   [TestFixture]
   public class RegistryOpsTests
   {
      [Test]
      public void TestHfmAutoRun()
      {
         RegistryOps.SetHfmAutoRun(System.Reflection.Assembly.GetExecutingAssembly().Location);
         Assert.AreEqual(true, RegistryOps.IsHfmAutoRunSet());
         RegistryOps.SetHfmAutoRun(String.Empty);
         Assert.AreEqual(false, RegistryOps.IsHfmAutoRunSet());
      }
   }
}
