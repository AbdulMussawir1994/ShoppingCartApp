namespace SalesOrderApi.Dtos;

public readonly record struct GetOrderDto(string OrderId, string productId, DateTime created, decimal qty,
                                                                                      string PName, decimal price, string consumer, string status, string userId);
