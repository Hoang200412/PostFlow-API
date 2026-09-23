using System;
using System.Collections.Generic;
using System.Linq;

namespace PostFlow.Models.DTOs.Responses
{
    /// <summary>
    /// Generic response wrapper cho dữ liệu phân trang
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của từng item</typeparam>
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }

        public PagedResult() { }

        public PagedResult(IEnumerable<T> items, int total, int page, int limit)
        {
            Items = items ?? Enumerable.Empty<T>();
            Total = total;
            Page = page;
            Limit = limit;
        }
    }
}
