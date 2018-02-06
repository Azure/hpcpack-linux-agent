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
                    json::value obj;

                    if (j.has_field(name))
                    {
                        obj = j.at(name);
                    }

                    return JsonHelper<T>::FromJson(obj);
                }

                static void Write(const std::string& name, json::value& v, const T& t)
                {
                    v[name] = ToJson(t);
                }

                static T FromJson(const json::value& j);

                static json::value ToJson(const T& t);

            protected:
            private:
        };

        // Partial specialize for vector.
        template <typename T>
        class JsonHelper<std::vector<T>>
        {
            public:
                static std::vector<T> Read(const std::string& name, const json::value& j)
                {
                    json::value obj;

                    if (j.has_field(name))
                    {
                        obj = j.at(name);
                    }

                    return JsonHelper<std::vector<T>>::FromJson(obj);
                }

                static std::vector<T> FromJson(const json::value& j)
                {
                    std::vector<T> values;

                    if (!j.is_null())
                    {
                        auto arr = JsonHelper<const json::array&>::FromJson(j);

                        std::transform(
                            arr.cbegin(),
                            arr.cend(),
                            std::back_inserter(values),
                            [](const auto& i) { return JsonHelper<T>::FromJson(i); });
                    }

                    return std::move(values);
                }

                static json::value ToJson(const std::vector<T>& vec)
                {
                    std::vector<json::value> values;

                    std::transform(
                        vec.cbegin(),
                        vec.cend(),
                        std::back_inserter(values),
                        [](const T& v) { return JsonHelper<T>::ToJson(v); });

                    return json::value::array(values);
                }

                static void Write(const std::string& name, json::value& v, const std::vector<T>& t)
                {
                    v[name] = ToJson(t);
                }

            protected:
            private:
        };

        // Partial specialize for map.
        template <typename T>
        class JsonHelper<std::map<std::string, T>>
        {
            public:
                static std::map<std::string, T> Read(const std::string& name, const json::value& j)
                {
                    json::value obj;

                    if (j.has_field(name))
                    {
                        obj = j.at(name);
                    }

                    return std::move(JsonHelper<std::map<std::string, T>>::FromJson(obj));
                }

                static std::map<std::string, T> FromJson(const json::value& j)
                {
                    std::map<std::string, T> values;

                    if (!j.is_null())
                    {
                        const auto& arr = JsonHelper<const json::object&>::FromJson(j);

                        for (auto const i : arr)
                        {
                            values[i.first] = JsonHelper<T>::FromJson(i.second);
                        }
                    }

                    return std::move(values);
                }

                static json::value ToJson(const std::map<std::string, T>& m)
                {
                    json::value v;

                    std::for_each(
                        m.cbegin(),
                        m.cend(),
                        [&v](const std::pair<std::string, T>& p)
                        {
                            v[p.first] = JsonHelper<T>::ToJson(p.second);
                        });

                    return std::move(v);
                }
       //         static void Write(const std::string& name, json::value& v, const std::vector<T>& t);

            protected:
            private:
        };
    }
}

#endif // JSONHELPER_H
