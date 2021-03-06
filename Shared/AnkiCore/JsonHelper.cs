﻿/*
Copyright (C) 2016 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Shared.AnkiCore
{
    public class JsonHelper
    {
        public static double GetNameNumber(JsonObject jobject, String name, double? defaultValue = null)
        {
            JsonValue idValue;
            if (defaultValue == null)
                idValue = jobject.GetNamedValue(name);
            else
                idValue = jobject.GetNamedValue(name, JsonValue.CreateNumberValue((double)defaultValue));
            double number;
            if (idValue.ValueType == JsonValueType.Number)
                number = idValue.GetNumber();
            else if (idValue.ValueType == JsonValueType.String)
                number = Convert.ToDouble(idValue.GetString());
            else
            {
                if (idValue.GetBoolean())
                    number = 1;
                else
                    number = 0;
            }
            return number;
        }
    }
}
