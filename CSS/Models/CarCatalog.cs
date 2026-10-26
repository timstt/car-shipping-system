using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ASTCUSA.Models
{
    // CarCatalog з діаграми класів: агрегує авто (CarCatalog 1 ◇— 0..* Car).
    // Не є таблицею в БД — працює поверх набору db.Cars.
    public class CarCatalog
    {
        private readonly IQueryable<Car> cars;

        public CarCatalog(IQueryable<Car> cars)
        {
            this.cars = cars;
        }

        // getAvailableCars(): Car[]
        public Car[] GetAvailableCars()
        {
            return cars.Where(c => c.Status == CarStatus.Available)
                       .OrderBy(c => c.Make).ThenBy(c => c.Model)
                       .ToArray();
        }

        // findCarByMake(make: String): Car[]
        public Car[] FindCarByMake(string make)
        {
            return Search(new CarSearchFilter { Make = make }, onlyAvailable: false);
        }

        // «Шукати за фільтром»: марка, модель, рік, діапазон ціни, статус
        public Car[] Search(CarSearchFilter filter, bool onlyAvailable)
        {
            var query = cars;
            filter = filter ?? new CarSearchFilter();

            if (onlyAvailable)
                query = query.Where(c => c.Status == CarStatus.Available);
            else if (filter.Status.HasValue)
                query = query.Where(c => c.Status == filter.Status.Value);

            if (!string.IsNullOrWhiteSpace(filter.Make))
                query = query.Where(c => c.Make.Contains(filter.Make.Trim()));
            if (!string.IsNullOrWhiteSpace(filter.Model))
                query = query.Where(c => c.Model.Contains(filter.Model.Trim()));
            if (filter.YearFrom.HasValue)
                query = query.Where(c => c.Year >= filter.YearFrom.Value);
            if (filter.YearTo.HasValue)
                query = query.Where(c => c.Year <= filter.YearTo.Value);
            if (filter.PriceFrom.HasValue)
                query = query.Where(c => c.AuctionPrice >= filter.PriceFrom.Value);
            if (filter.PriceTo.HasValue)
                query = query.Where(c => c.AuctionPrice <= filter.PriceTo.Value);

            return query.OrderBy(c => c.Make).ThenBy(c => c.Model).ToArray();
        }

        // Для сторінки «Обране»
        public Car[] GetByIds(ICollection<int> ids)
        {
            var idArray = ids.ToArray();
            return cars.Where(c => idArray.Contains(c.CarId)).ToArray();
        }
    }

    public class CarSearchFilter
    {
        [Display(Name = "Марка")]
        public string Make { get; set; }

        [Display(Name = "Модель")]
        public string Model { get; set; }

        [Display(Name = "Рік від")]
        public int? YearFrom { get; set; }

        [Display(Name = "Рік до")]
        public int? YearTo { get; set; }

        [Display(Name = "Ціна від, $")]
        public double? PriceFrom { get; set; }

        [Display(Name = "Ціна до, $")]
        public double? PriceTo { get; set; }

        [Display(Name = "Статус")]
        public CarStatus? Status { get; set; }
    }

    // Модель сторінки каталогу: фільтр + результати
    public class CarCatalogViewModel
    {
        public CarSearchFilter Filter { get; set; }
        public Car[] Cars { get; set; }
        public ICollection<int> FavoriteIds { get; set; }
    }
}
