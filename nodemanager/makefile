# node manager makefile.

# casablanca
CASABLANCA = ../../evancasa
CASA_RELEASE = $(CASABLANCA)/Release
CASA_INC = $(CASA_RELEASE)/include
CASA_LIB_DEBUG = $(CASA_RELEASE)/build.debug/Binaries
CASA_LIB_RELEASE = $(CASA_RELEASE)/build.release/Binaries

# spdlog
SPDLOG = ../../evanspdlog
SPDLOG_INC = $(SPDLOG)/include

# tools
CC = gcc
CXX = g++
AR = ar
LD = g++
WINDRES = windres

# common
LIBOUTDIR = lib
INSTALLDIR = /opt/hpcnodemanager
INC = -I$(CASA_INC) -I$(SPDLOG_INC)
CFLAGS = -Wall -std=c++14 -Wno-unused-local-typedefs
LIB = -lcpprest -lpthread -lboost_system -lssl -lcrypto -lfmt
LDFLAGS = -Wl,-rpath,\$$ORIGIN/$(LIBOUTDIR),-I$(INSTALLDIR)/$(LIBOUTDIR)/ld-linux-x86-64.so.2
BINARY = nodemanager
DEBUG = debug
RELEASE = release
OBJDIR = obj
OUTDIR = bin
CPPSWITHDIR = $(wildcard */*.cpp *.cpp)
CPPS = $(notdir $(CPPSWITHDIR))
OBJS = $(CPPS:.cpp=.o)

# debug
MACRO_DEBUG = -D DEBUG
INC_DEBUG = $(INC)
CFLAGS_DEBUG = $(CFLAGS) -g
LIBDIR_DEBUG = -L$(CASA_LIB_DEBUG)
LIB_DEBUG = $(LIB)
LDFLAGS_DEBUG = $(LDFLAGS)
OBJDIR_DEBUG = $(OBJDIR)/$(DEBUG)
OBJDIRSED_DEBUG = $(OBJDIR)\/$(DEBUG)
OUTDIR_DEBUG = $(OUTDIR)/$(DEBUG)
OUT_DEBUG = $(OUTDIR_DEBUG)/$(BINARY)
OBJS_DEBUG = $(addprefix $(OBJDIR_DEBUG)/, $(OBJS))

# release
MACRO_RELEASE =
INC_RELEASE = $(INC)
CFLAGS_RELEASE = $(CFLAGS) -O2
LIBDIR_RELEASE = -L$(CASA_LIB_RELEASE)
LIB_RELEASE = $(LIB)
LDFLAGS_RELEASE = $(LDFLAGS) -s
OBJDIR_RELEASE = $(OBJDIR)/$(RELEASE)
OBJDIRSED_RELEASE = $(OBJDIR)\/$(RELEASE)
OUTDIR_RELEASE = $(OUTDIR)/$(RELEASE)
OUT_RELEASE = $(OUTDIR_RELEASE)/$(BINARY)
OBJS_RELEASE = $(addprefix $(OBJDIR_RELEASE)/, $(OBJS))

# pseudo targets
all: debug release
rebuild: clean all
clean: clean_debug clean_release
release: before_release out_release after_release
debug: before_debug out_debug after_debug
redebug: clean_debug debug
rerelease: clean_release release

# color output
define compile
	$(indent);
	@tput bold;
	@echo -n "Compiling [ ";
	@tput setaf 6;
	@echo -n $@;
	@tput setaf 7;
	@echo -n " ] using [ ";
	@tput setaf 5;
	@echo -n $<;
	@tput setaf 7;
	@echo " ]";
	@tput sgr0
	@$(CXX) $(1) $(2) $(3) -c $< -o $@
endef

define building
	$(indent);
	@tput bold;
	@echo -n "Building [ ";
	@tput setaf 6;
	@echo -n $(1);
	@tput setaf 7;
	@echo " ]";
	@tput sgr0
endef

indent=@printf '\t'

define prepare
	@echo ""
	@tput bold;
	@echo -n "Preparing to build [ ";
	@tput setaf 2;
	@echo -n $(1);
	@tput setaf 7;
	@echo " ] target";
	@tput sgr0;
	$(indent);
	[ -d $(2) ] || mkdir -p $(2)
	$(indent);
	[ -d $(3) ] || mkdir -p $(3)
	@echo ""
	@tput bold;
	@echo -n "Start to build [ ";
	@tput setaf 2;
	@echo -n $(1);
	@tput setaf 7;
	@echo " ] target";
	@tput sgr0;
endef

define finish
	$(indent);
	cp config/nodemanager.json $(2)/nodemanager.json.sample
	$(indent);
	cp scripts/* $(2)/
	$(indent);
	[ -d $(2)/filters ] || mkdir $(2)/filters
	$(indent);
	cp filters/*.sh $(2)/filters
	$(indent);
	[ -d $(2)/logs ] || mkdir $(2)/logs
	$(indent);
	[ -d $(2)/$(LIBOUTDIR) ] || mkdir $(2)/$(LIBOUTDIR)
	$(indent);
	cp $(shell ldd $(3) | cut -f3 -d' ') $(2)/$(LIBOUTDIR) 2> /dev/null || :
	@tput bold;
	@echo -n "Finished to build [ ";
	@tput setaf 2;
	@echo -n $(1);
	@tput setaf 7;
	@echo " ] target";
	@tput sgr0;
endef

# debug targets
before_debug:
	$(call prepare,$(DEBUG),$(OUTDIR_DEBUG),$(OBJDIR_DEBUG))

after_debug:
	$(call finish,$(DEBUG),$(OUTDIR_DEBUG),$(OUT_DEBUG))

out_debug: before_debug $(OUT_DEBUG)

$(OUT_DEBUG): $(OBJS_DEBUG)
	$(call building,$@)
	$(indent);
	$(LD) $(LIBDIR_DEBUG) -o $@ $(OBJDIR_DEBUG)/*.o $(LDFLAGS_DEBUG) $(LIB_DEBUG)

clean_debug:
	rm -rf $(OUTDIR_DEBUG)
	rm -rf $(OBJDIR_DEBUG)

$(OBJDIR_DEBUG)/%.o: */%.cpp
	$(call compile,$(CFLAGS_DEBUG),$(INC_DEBUG),$(MACRO_DEBUG))

$(OBJDIR_DEBUG)/%.o: %.cpp
	$(call compile,$(CFLAGS_DEBUG),$(INC_DEBUG),$(MACRO_DEBUG))

# release targets
before_release:
	$(call prepare,$(RELEASE),$(OUTDIR_RELEASE),$(OBJDIR_RELEASE))

after_release:
	$(call finish,$(RELEASE),$(OUTDIR_RELEASE),$(OUT_RELEASE))

out_release: before_release $(OUT_RELEASE)

$(OUT_RELEASE): $(OBJS_RELEASE)
	$(call building,$@)
	$(indent);
	$(LD) $(LIBDIR_RELEASE) -o $@ $(OBJDIR_RELEASE)/*.o $(LDFLAGS_RELEASE) $(LIB_RELEASE)

clean_release:
	rm -rf $(OUTDIR_RELEASE)
	rm -rf $(OBJDIR_RELEASE)

$(OBJDIR_RELEASE)/%.o: */%.cpp
	$(call compile,$(CFLAGS_RELEASE),$(INC_RELEASE),$(MACRO_RELEASE))

$(OBJDIR_RELEASE)/%.o: %.cpp
	$(call compile,$(CFLAGS_RELEASE),$(INC_RELEASE),$(MACRO_RELEASE))

# phony targets
.PHONY: before_debug after_debug clean_debug before_release after_release clean_release

# dependency
include deps
deps: $(CPPSWITHDIR)
	@tput bold
	@echo "Rebuilding dependencies ... "
	@tput sgr0
	$(indent)
	$(CXX) $(CFLAGS) -MM *.cpp */*.cpp > rawdeps
	$(indent)
	cat rawdeps | sed -e "s/\(.*\)\.o/$(OBJDIRSED_DEBUG)\/\1\.o/" > deps
	$(indent)
	cat rawdeps | sed -e "s/\(.*\)\.o/$(OBJDIRSED_RELEASE)\/\1\.o/" >> deps
	@tput setaf 2
	@tput bold
	@echo "Done."
	@tput sgr0;
