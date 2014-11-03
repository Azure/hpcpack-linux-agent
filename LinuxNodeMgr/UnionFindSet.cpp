#include "UnionFindSet.h"

UnionFindSet::UnionFindSet()
{
    //ctor
    Parents.clear();
}

UnionFindSet::~UnionFindSet()
{
    //dtor
}

void UnionFindSet::AddPair(int child, int parent)
{
    if (Parents.find(parent) == Parents.end()) Parents[parent] = parent;
    if (Parents.find(child) == Parents.end()) Parents[child] = parent;
    else Parents[FindParent(child)] = FindParent(parent);
}
int UnionFindSet::FindParent(int child)
{
    if (Parents[child] == child) return child;
    else return Parents[child] = FindParent(Parents[child]);
}
