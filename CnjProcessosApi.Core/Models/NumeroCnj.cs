using System.Text.RegularExpressions;

namespace CnjProcessosApi.Core.Models;

public static class NumeroCnj
{
    private static readonly string[] Estados =
        ["", "tjac", "tjal", "tjap", "tjam", "tjba", "tjce", "tjdft", "tjes", "tjgo", "tjma", "tjmt", "tjms", "tjmg", "tjpa", "tjpb", "tjpr", "tjpe", "tjpi", "tjrj", "tjrn", "tjrs", "tjro", "tjrr", "tjsc", "tjse", "tjsp", "tjto"];

    public static string Normalizar(string valor)
    {
        valor = valor.Trim();
        if (!Regex.IsMatch(valor, @"^(?:[0-9]{20}|[0-9]{7}-[0-9]{2}\.[0-9]{4}\.[0-9]\.[0-9]{2}\.[0-9]{4})$"))
            throw new ArgumentException("Informe um número CNJ com 20 dígitos, com ou sem pontuação.");
        var numero = valor.Replace(".", "").Replace("-", "");
        var resto = 0;
        foreach (var digito in numero[..7] + numero[9..] + "00") resto = (resto * 10 + digito - '0') % 97;
        if (98 - resto != int.Parse(numero.Substring(7, 2)))
            throw new ArgumentException("O dígito verificador do número CNJ é inválido.");
        return numero;
    }

    public static string Tribunal(string numero)
    {
        var codigo = int.Parse(numero.Substring(14, 2));
        var alias = numero[13] switch
        {
            '8' when codigo is >= 1 and <= 27 => Estados[codigo],
            '4' when codigo is >= 1 and <= 6 => $"trf{codigo}",
            '5' when codigo is >= 1 and <= 24 => $"trt{codigo}",
            '5' when codigo == 0 => "tst",
            '3' when codigo == 0 => "stj",
            '6' when codigo == 0 => "tse",
            '6' when codigo is >= 1 and <= 27 => "tre-" + Estados[codigo][2..],
            '7' when codigo == 0 => "stm",
            '9' when codigo == 13 => "tjmmg",
            '9' when codigo == 21 => "tjmrs",
            '9' when codigo == 26 => "tjmsp",
            _ => throw new ArgumentException("Tribunal não atendido pelo mapeamento da API Pública do DataJud.")
        };
        return $"api_publica_{alias}/_search";
    }
}
