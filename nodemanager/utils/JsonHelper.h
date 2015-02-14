#ifndef JSONHELPER_H
#define JSONHELPER_H

#include <vector>
#include <map>

#include <cpprest/json.h>

using namespace web;

namespace hpc
{
    namespace utils
    {
        template <typename T>
        class JsonHelper
        {
            public:
                static T Read(const std::string& name, const json::value& j)
                {
                    auto obj = j.at(name);
                    return JsonHelper<T>::GetValue(obj);
                }

                static void Write(const std::string& name, json::value& v, const T& t)
                {
                    v[name] = GetJson(t);
                }

                static T GetValue(const json::value& j);

                // Implicit interface on T
                static json::value&& GetJson(const T& t) { return std::move(T::ToJson(t)); }

            protected:
            private:
        };

        // Partial specialize for vector.
        template <typename T>
        class JsonHelper<std::vector<T>>
        {
            public:
                static std::vector<T>&& Read(const std::string& name, const json::value& j)
                {
                    auto obj = j.at(name);
                    return JsonHelper<std::vector<T>>::GetValue(j);
                }

                static std::vector<T>&& GetValue(const json::value& j)
                {
                    auto arr = JsonHelper<const json::array&>::GetValue(j);

                    std::vector<T> values;

                    std::transform(
                        arr.cbegin(),
                        arr.cend(),
                        std::back_inserter(values),
                        [](const json::value& i) { return JsonHelper<T>::GetValue(i); });

                    return std::move(values);
                }

                static json::array&& GetJson(const std::vector<T>& vec)
                {
                    std::vector<json::value> values;

                    std::transform(
                        vec.cbegin(),
                        vec.cend(),
                        std::back_inserter(values),
                        [](const T& v) { return JsonHelper<T>::GetJson(v); });

                    return json::value::array(values);
                }

            protected:
            private:
        };

        // Partial specialize for map.
        template <typename T>
        class JsonHelper<std::map<std::string, T>>
        {
            public:
                static std::map<std::string, T>&& Read(const std::string& name, const json::value& j)
                {
                    auto obj = j.at(name);
                    return JsonHelper<std::map<std::string, T>>::GetValue(j);
                }

                static std::map<std::string, T>&& GetValue(const json::value& j)
                {
                    const auto& arr = JsonHelper<const json::object&>::GetValue(j);

                    std::map<std::string, T> values;

                    for (auto const i : arr)
                    {
                        values[i.first] = JsonHelper<T>::GetValue(i.second);
                    }

                    return std::move(values);
                }
//
//                static json::object&& GetJson(const std::map<std::string, T>& m)
//                {
//                    return json::value::object(m);
//                }
       //         static void Write(const std::string& name, json::value& v, const std::vector<T>& t);

            protected:
            private:
        };
    }
}

#endif // JSONHELPER_H
