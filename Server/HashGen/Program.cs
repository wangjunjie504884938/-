using System;
var hash = BCrypt.Net.BCrypt.HashPassword("123456");
Console.WriteLine(hash);
