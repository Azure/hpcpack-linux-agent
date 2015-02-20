#include "JsonHelper.h"

namespace hpc
{
    /// GetValue/GetJson specializations.
    namespace utils
    {
        #define GETVALUE(T, Check, Fetch) \
        template <> \
        T JsonHelper<T>::GetValue(const json::value& j) \
        { \
            if (!j.is_null() && j.Check) { return j.Fetch; } \
            throw std::runtime_error("cannot get value from json object"); \
        }

        GETVALUE(int, is_number(), as_integer())
        GETVALUE(long, is_number(), as_number().to_int64())
        GETVALUE(std::string, is_string(), as_string())
        GETVALUE(const json::array&, is_array(), as_array())
        GETVALUE(long long, is_number(), as_number().to_int64())
        GETVALUE(double, is_number(), as_number().to_double())
        GETVALUE(bool, is_boolean(), as_bool())
        GETVALUE(const json::object&, is_object(), as_object())

        #define GETJSON(T, JsonType) \
        template <> \
        json::value&& JsonHelper<T>::GetJson(const T& val) { return std::move(json::value::JsonType(val)); }

        GETJSON(int, number)
        GETJSON(double, number)
        GETJSON(bool, boolean)
        GETJSON(std::string, string)
    }
}

