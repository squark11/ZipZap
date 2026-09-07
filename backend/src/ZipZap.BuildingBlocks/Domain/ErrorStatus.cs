namespace ZipZap.BuildingBlocks.Domain;

/// <summary>Mapowanie kodu błędu domenowego na kod HTTP (używane przez endpointy).</summary>
public static class ErrorStatus
{
    public static int ToStatusCode(this Error error) => error.Code switch
    {
        "not_found" => 404,
        "validation" => 400,
        "conflict" => 409,
        "unauthorized" => 401,
        "forbidden" => 403,
        _ => 500,
    };
}
