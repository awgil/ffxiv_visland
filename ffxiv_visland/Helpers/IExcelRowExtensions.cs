using ECommons.DalamudServices;
using Lumina.Excel;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace visland.Helpers;

public static class IExcelRowExtensions {
    extension<T>(IExcelRow<T> excelRow) where T : struct, IExcelRow<T> {
        public T WithLanguage(Dalamud.Game.ClientLanguage language) => Get<T>(language: language).GetRow(excelRow.RowId);
        public T WithLanguage(Lumina.Data.Language language) => Get<T>(language: (Dalamud.Game.ClientLanguage)language).GetRow(excelRow.RowId);
        public static ExcelSheet<T> Get(Dalamud.Game.ClientLanguage? language = null) => Service.DataManager.GetExcelSheet<T>(language);
        public static T? GetRow(uint rowId) => Get<T>().GetRowOrDefault(rowId);
        public static bool Any(Func<T, bool> predicate) => Get<T>().Any(r => predicate(r));
    }

    extension<T>(IExcelSubrow<T> subRow) where T : struct, IExcelSubrow<T> {

        public static T? FirstOrNull() => EnumerateSubrows<T>().FirstOrNull();
        public static T? FirstOrNull(Func<T, bool> predicate) => EnumerateSubrows<T>().Where(predicate).FirstOrNull();
    }

    private static IEnumerable<T> EnumerateSubrows<T>(Dalamud.Game.ClientLanguage? language = null) where T : struct, IExcelSubrow<T>
        => Svc.Data.GetSubrowExcelSheet<T>(language: language).SelectMany(r => r);
}
