#ifndef UNIONFINDSET_H
#define UNIONFINDSET_H
#include<map>

class UnionFindSet
{
    public:
        UnionFindSet();
        virtual ~UnionFindSet();
        void AddPair(int child, int parent);
        int FindParent(int child);

    private:
        std::map<int,int> Parents;
};

#endif // UNIONFINDSET_H
